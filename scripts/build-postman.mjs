// Authoring utility. Generated JSON files are ready to import; Node is not needed by API users.
import { mkdirSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
const directory = fileURLToPath(new URL('../postman/', import.meta.url));
mkdirSync(directory, { recursive: true });
const event = (listen, lines) => ({ listen, script: { type: 'text/javascript', exec: lines } });
const status = code => `pm.test('HTTP ${code}', () => pm.response.to.have.status(${code}));`;
function request(name, method, path, code, { body, token = 'adminToken', tests = [], before = [] } = {}) {
  const raw = `{{baseUrl}}/api/v1${path}`;
  const req = { method, url: raw, header: [] };
  req.auth = token ? { type: 'bearer', bearer: [{ key: 'token', value: `{{${token}}}`, type: 'string' }] } : { type: 'noauth' };
  if (body !== undefined) {
    req.header.push({ key: 'Content-Type', value: 'application/json' });
    req.body = { mode: 'raw', raw: typeof body === 'string' ? body : JSON.stringify(body, null, 2), options: { raw: { language: 'json' } } };
  }
  const item = { name, request: req, event: [event('test', [status(code), ...tests])] };
  if (before.length) item.event.unshift(event('prerequest', before));
  return item;
}
const remember = key => [`pm.collectionVariables.set('${key}', pm.response.json().id);`];
const book = { inventoryNumber: 'PM-{{runId}}', title: 'Книга Postman {{runId}}', author: 'Учебный автор', genre: 'Тест', publicationYear: 2026 };
const reader = { cardNumber: 'PM-{{runId}}', fullName: 'Читатель Postman {{runId}}', email: 'postman@example.test' };
const issue = '{"bookId": {{bookId}}, "readerId": {{readerId}}}';
const reportTests = [
  "pm.test('Постраничный результат', () => { const b = pm.response.json(); pm.expect(b.items).to.be.an('array'); pm.expect(b.totalCount).to.be.a('number'); });"
];
const folders = [
  { name: '01 — Вход и базовые права', item: [
    request('01 Вход Admin; начало нового прогона', 'POST', '/auth/login', 200, {
      token: null, body: { login: '{{adminLogin}}', password: '{{adminPassword}}' },
      before: ["pm.collectionVariables.set('runId', Date.now().toString(36) + Math.random().toString(36).slice(2,7));"],
      tests: ["pm.collectionVariables.set('adminToken', pm.response.json().accessToken);", "pm.test('Роль Admin', () => pm.expect(pm.response.json().user.role).to.eql('Admin'));" ]
    }),
    request('02 Вход User', 'POST', '/auth/login', 200, { token: null, body: { login: '{{userLogin}}', password: '{{userPassword}}' },
      tests: ["pm.collectionVariables.set('userToken', pm.response.json().accessToken);"] }),
    request('03 Каталог без токена', 'GET', '/books', 200, { token: null, tests: reportTests }),
    request('04 Отчёт без токена — 401', 'GET', '/reports/active-loans', 401, { token: null,
      tests: ["pm.test('Bearer challenge', () => pm.expect(pm.response.headers.get('WWW-Authenticate')).to.include('Bearer'));" ] }),
    request('05 Создание с ролью User — 403', 'POST', '/books', 403, { token: 'userToken', body: book }),
    request('06 Изменение с ролью User — 403', 'PUT', '/books/1', 403, { token: 'userToken', body: { title: 'Название', author: 'Автор', genre: 'Тест', publicationYear: 2026 } }),
    request('07 Удаление с ролью User — 403', 'DELETE', '/books/1', 403, { token: 'userToken' }),
    request('08 Неверный пароль — 401', 'POST', '/auth/login', 401, { token: null, body: { login: '{{adminLogin}}', password: 'incorrect-password' } }),
    request('09 Текущий пользователь', 'GET', '/auth/me', 200, { token: 'userToken',
      tests: ["pm.test('Роль User', () => pm.expect(pm.response.json().role).to.eql('User'));", "pm.test('Нет passwordHash', () => pm.expect(pm.response.json()).not.to.have.property('passwordHash'));" ] })
  ] },
  { name: '02 — Книги: CRUD и архив', item: [
    request('10 Добавить экземпляр', 'POST', '/books', 201, { body: book, tests: [...remember('bookId'), "pm.test('Location', () => pm.expect(pm.response.headers.get('Location')).to.include('/books/'));" ] }),
    request('11 Прочитать экземпляр', 'GET', '/books/{{bookId}}', 200, { tests: ["pm.test('Доступен', () => pm.expect(pm.response.json().isAvailable).to.eql(true));"] }),
    request('12 Изменить описание', 'PUT', '/books/{{bookId}}', 200, { body: { title: 'Обновлённая книга {{runId}}', author: 'Учебный автор', genre: 'Тест', publicationYear: 2025 } }),
    request('13 Дубликат инвентарного номера — 409', 'POST', '/books', 409, { body: book }),
    request('14 Поиск и пагинация', 'GET', '/books?search={{runId}}&page=1&pageSize=10', 200, {
      tests: ["pm.test('Найден тестовый экземпляр', () => pm.expect(pm.response.json().items.some(x => x.id === Number(pm.collectionVariables.get('bookId')))).to.eql(true));"] }),
    request('15 Архивировать', 'POST', '/books/{{bookId}}/archive', 200, { tests: ["pm.test('Архивный экземпляр недоступен', () => { pm.expect(pm.response.json().isArchived).to.eql(true); pm.expect(pm.response.json().isAvailable).to.eql(false); });"] }),
    request('16 Восстановить', 'POST', '/books/{{bookId}}/restore', 200)
  ] },
  { name: '03 — Читатели: CRUD и блокировка', item: [
    request('17 Добавить читателя', 'POST', '/readers', 201, { body: reader, tests: remember('readerId') }),
    request('18 Прочитать читателя', 'GET', '/readers/{{readerId}}', 200),
    request('19 Изменить контакты', 'PUT', '/readers/{{readerId}}', 200, { body: { fullName: 'Обновлённый читатель {{runId}}', email: 'updated@example.test' } }),
    request('20 Найти читателя', 'GET', '/readers?search={{runId}}', 200, { tests: reportTests }),
    request('21 Заблокировать', 'POST', '/readers/{{readerId}}/block', 200),
    request('22 Выдача заблокированному читателю — 409', 'POST', '/loans', 409, { body: issue }),
    request('23 Разблокировать', 'POST', '/readers/{{readerId}}/unblock', 200),
    request('24 Архивировать перед проверкой выдачи', 'POST', '/books/{{bookId}}/archive', 200),
    request('25 Выдача архивной книги — 409', 'POST', '/loans', 409, { body: issue }),
    request('26 Восстановить перед выдачей', 'POST', '/books/{{bookId}}/restore', 200),
    request('26a Найти демонстрационного читателя у лимита', 'GET', '/readers?search=R-004', 200, {
      tests: ["pm.test('Читатель найден', () => pm.expect(pm.response.json().items.length).to.eql(1));", "pm.collectionVariables.set('limitReaderId', pm.response.json().items[0].id);"] }),
    request('26b Выдача сверх лимита — 409', 'POST', '/loans', 409, {
      body: '{"bookId": {{bookId}}, "readerId": {{limitReaderId}}}' })
  ] },
  { name: '04 — Выдачи: создание, чтение и изменение', item: [
    request('27 Выдать книгу', 'POST', '/loans', 201, { body: issue, tests: remember('loanId') }),
    request('28 Прочитать выдачу', 'GET', '/loans/{{loanId}}', 200),
    request('29 Список выдач читателя', 'GET', '/loans?readerId={{readerId}}&activeOnly=true', 200, { tests: reportTests }),
    request('30 Изменить срок возврата', 'PUT', '/loans/{{loanId}}', 200, {
      before: ["pm.collectionVariables.set('futureDue', new Date(Date.now() + 20 * 86400000).toISOString());"],
      body: { dueAtUtc: '{{futureDue}}' } }),
    request('31 Повторная выдача экземпляра — 409', 'POST', '/loans', 409, { body: issue }),
    request('32 Удаление активной выдачи — 409', 'DELETE', '/loans/{{loanId}}', 409),
    request('33 Архивирование выданной книги — 409', 'POST', '/books/{{bookId}}/archive', 409),
    request('34 Удаление книги с историей — 409', 'DELETE', '/books/{{bookId}}', 409),
    request('35 Удаление читателя с историей — 409', 'DELETE', '/readers/{{readerId}}', 409)
  ] },
  { name: '05 — Четыре отчёта с User-токеном', item: [
    request('36 Активные выдачи — три класса', 'GET', '/reports/active-loans', 200, { token: 'userToken', tests: [...reportTests,
      "pm.test('Объединены данные', () => { const b = pm.response.json(); pm.expect(b.items.length).to.be.above(0); pm.expect(b.items[0]).to.include.all.keys('bookTitle', 'readerName', 'dueAtUtc'); });"] }),
    request('37 Просроченные выдачи — три класса', 'GET', '/reports/overdue-loans', 200, { token: 'userToken', tests: [...reportTests,
      "pm.test('Просрочка положительна', () => pm.response.json().items.forEach(x => pm.expect(x.overdueDays).to.be.above(0)));" ] }),
    request('38 Популярность экземпляров', 'GET', '/reports/popular-books', 200, { token: 'userToken', tests: [...reportTests,
      "pm.test('Количество выдач', () => pm.response.json().items.forEach(x => pm.expect(x.loanCount).to.be.above(0)));" ] }),
    request('39 Статистика читателей', 'GET', '/reports/reader-statistics', 200, { token: 'userToken', tests: [...reportTests,
      "pm.test('Суммы согласованы', () => pm.response.json().items.forEach(x => pm.expect(x.totalLoans).to.eql(x.activeLoans + x.returnedLoans)));" ] }),
    request('40 Статистика за пустой период', 'GET', '/reports/reader-statistics?from=2090-01-01T00:00:00Z&to=2091-01-01T00:00:00Z', 200, { token: 'userToken',
      tests: ["pm.test('Читатели без выдач сохранены', () => { const b = pm.response.json(); pm.expect(b.items.length).to.be.above(0); b.items.forEach(x => pm.expect(x.totalLoans).to.eql(0)); });"] })
  ] },
  { name: '06 — Проверки входных данных', item: [
    request('41 Неполная книга — 400', 'POST', '/books', 400, { body: { title: '' } }),
    request('42 Недопустимая пагинация — 400', 'GET', '/books?pageSize=101', 400),
    request('43 Книга не существует — 404', 'GET', '/books/2147483647', 404),
    request('44 Неверный период — 400', 'GET', '/reports/popular-books?from=2030-01-01T00:00:00Z&to=2020-01-01T00:00:00Z', 400),
    request('45 Запрещённое поле JSON — 400', 'PUT', '/readers/{{readerId}}', 400, { body: { fullName: 'Имя', email: null, isBlocked: false } }),
    request('46 Некорректный срок — 400', 'PUT', '/loans/{{loanId}}', 400, { body: { dueAtUtc: '2000-01-01T00:00:00Z' } }),
    request('46a Испорченный JWT — 401', 'GET', '/reports/active-loans', 401, { token: 'invalidToken' })
  ] },
  { name: '07 — Возврат и очистка тестовых записей', item: [
    request('47 Вернуть книгу', 'POST', '/loans/{{loanId}}/return', 200, { tests: ["pm.test('Возврат зафиксирован', () => pm.expect(pm.response.json().returnedAtUtc).to.be.a('string'));" ] }),
    request('48 Повторный возврат — 409', 'POST', '/loans/{{loanId}}/return', 409),
    request('49 Изменение завершённой выдачи — 409', 'PUT', '/loans/{{loanId}}', 409, { body: { dueAtUtc: '{{futureDue}}' } }),
    request('50 Книга снова доступна', 'GET', '/books/{{bookId}}', 200, { tests: ["pm.test('Доступна', () => pm.expect(pm.response.json().isAvailable).to.eql(true));"] }),
    request('51 Удалить завершённую выдачу', 'DELETE', '/loans/{{loanId}}', 204),
    request('52 Удалить экземпляр', 'DELETE', '/books/{{bookId}}', 204),
    request('53 Удалить читателя', 'DELETE', '/readers/{{readerId}}', 204),
    request('54 Удалённая выдача — 404', 'GET', '/loans/{{loanId}}', 404)
  ] }
];
const collection = {
  info: { _postman_id: 'c29e752e-c383-4a5d-b47e-f724d081b839', name: 'Library API — лабораторная, полный прогон',
    description: 'Запускайте все папки по порядку через Collection Runner. Создаёт собственные данные и удаляет их в конце. Токены и ID сохраняются автоматически. Начальная библиотека не изменяется. Аккаунты относятся только к локальной учебной демонстрации.',
    schema: 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json' },
  variable: [...['runId', 'adminToken', 'userToken', 'bookId', 'readerId', 'loanId', 'futureDue', 'limitReaderId'].map(key => ({ key, value: '', type: 'string' })),
    { key: 'invalidToken', value: 'bad.token.value', type: 'string' }],
  item: folders
};
const environment = {
  id: '41ae94f6-6065-4db3-a5ec-d82ee3b03de2', name: 'Library API — localhost',
  values: [ ['baseUrl', 'http://localhost:5080', 'default'], ['adminLogin', 'admin', 'default'],
    ['adminPassword', 'Admin-Library-2026!', 'secret'], ['userLogin', 'user', 'default'], ['userPassword', 'User-Library-2026!', 'secret'] ]
    .map(([key, value, type]) => ({ key, value, type, enabled: true })),
  _postman_variable_scope: 'environment'
};
writeFileSync(directory + 'LibraryApi.postman_collection.json', JSON.stringify(collection, null, 2) + '\n');
writeFileSync(directory + 'LibraryApi.local.postman_environment.json', JSON.stringify(environment, null, 2) + '\n');
console.log(`Generated ${folders.reduce((sum, f) => sum + f.item.length, 0)} Postman requests.`);
