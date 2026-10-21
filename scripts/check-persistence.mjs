// Two-phase smoke check of a running release. Stores only the test ID, never a token.
import { readFileSync, writeFileSync, unlinkSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
const phase = process.argv[2];
if (!['create', 'verify'].includes(phase)) throw new Error('Use create, restart API, then verify.');
const base = process.env.LIBRARY_BASE_URL ?? 'http://localhost:5080';
const markerPath = fileURLToPath(new URL('../.local/persistence-marker.json', import.meta.url));
async function call(path, method = 'GET', body, token) {
  const response = await fetch(base + '/api/v1' + path, {
    method, headers: { ...(body ? { 'Content-Type': 'application/json' } : {}), ...(token ? { Authorization: 'Bearer ' + token } : {}) },
    body: body ? JSON.stringify(body) : undefined
  });
  if (!response.ok) throw new Error(`Unexpected ${response.status} for ${method} ${path}`);
  return response.status === 204 ? null : response.json();
}
const login = await call('/auth/login', 'POST', { login: 'admin', password: 'Admin-Library-2026!' });
if (phase === 'create') {
  const inventoryNumber = 'PERSIST-' + Date.now().toString(36);
  const book = await call('/books', 'POST', { inventoryNumber, title: 'Проверка перезапуска', author: 'Тест', genre: 'Тест', publicationYear: 2026 }, login.accessToken);
  writeFileSync(markerPath, JSON.stringify({ id: book.id, inventoryNumber }));
  console.log('Persistence marker created. Restart the release before verify.');
} else {
  const marker = JSON.parse(readFileSync(markerPath, 'utf8'));
  const book = await call('/books/' + marker.id, 'GET', undefined, login.accessToken);
  if (book.inventoryNumber !== marker.inventoryNumber) throw new Error('Persistence mismatch');
  await call('/books/' + marker.id, 'DELETE', undefined, login.accessToken);
  unlinkSync(markerPath);
  const catalog = await call('/books?includeArchived=true');
  const readers = await call('/readers', 'GET', undefined, login.accessToken);
  const loans = await call('/loans', 'GET', undefined, login.accessToken);
  if (catalog.totalCount !== 11 || readers.totalCount !== 5 || loans.totalCount !== 8)
    throw new Error('Unexpected counts after cleanup');
  console.log('Persistence verified after restart; cleanup verified: 11 books, 5 readers, 8 loans.');
}
