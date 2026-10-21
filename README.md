# Library API — библиотека

Лабораторная работа по дисциплине «Технологии кроссплатформенной разработки». Серверное Web API на C# / ASP.NET Core 9 с контроллерами, SQLite, EF Core, LINQ и JWT. Проект рассчитан на Visual Studio 2022 17.14 и последующее подключение клиентского приложения.

## Быстрый запуск исходников

Требуются Visual Studio 2022 17.14 с компонентом ASP.NET или .NET SDK 9.0.306 и новее в ветке 9.0.3xx. Если установлен другой совместимый SDK 9, скорректируйте global.json под него. Пакеты восстанавливаются из NuGet.

Из корня проекта в PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup-demo.ps1
dotnet restore LibraryApi.sln
dotnet run --project .\src\LibraryApi
```

Откройте http://localhost:5080/swagger. В Visual Studio после выполнения setup-demo.ps1 достаточно открыть LibraryApi.sln, назначить LibraryApi запускаемым проектом и нажать F5. Настройки первого запуска генерируются один раз в игнорируемом файле .local/demo-settings.json.

Альтернативный запуск из исходников с явно выбранным портом:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-demo.ps1 -Port 5080
```

При первом запуске автоматически применяются миграции и создаётся демонстрационная библиотека. БД: src/LibraryApi/App_Data/library.db. При повторном запуске данные сохраняются, начальные записи не дублируются. Начальная просрочка рассчитывается относительно первого заполнения БД.

## Запуск готового релиза

1. Скачайте LibraryApi-1.0.0-win-x64.zip из GitHub Releases и распакуйте в каталог с правом записи.
2. В каталоге архива выполните:

```powershell
powershell -ExecutionPolicy Bypass -File .\start-release.ps1
```

3. Откройте http://localhost:5080/swagger.

SDK, Visual Studio и отдельно установленный .NET для готового win-x64 архива не нужны: runtime включён. SQLite находится в app/App_Data/library.db. Локальные настройки создаются в .local рядом со start-release.ps1. Для остановки нажмите Ctrl+C. Если порт занят, передайте -Port 5081 и измените baseUrl в Postman.

Исходная копия scripts/start-release.ps1 используется при упаковке; запускайте start-release.ps1 из распакованного комплекта.

## Демонстрационные аккаунты

| Логин | Пароль | Роль |
|---|---|---|
| admin | Admin-Library-2026! | Admin |
| user | User-Library-2026! | User |

Это общедоступные учебные аккаунты локальной демонстрации. В БД хранятся хеши паролей, а не пароли. JWT-ключ каждый запуск setup-demo для нового комплекта генерирует случайно; существующий файл настроек не перезаписывается. Не публикуйте локальный JSON, БД или токены. Изменение Demo:*Password после создания аккаунтов не меняет существующие хеши.

Вход: POST /api/v1/auth/login с JSON:

```json
{ "login": "admin", "password": "Admin-Library-2026!" }
```

Ответ содержит accessToken, expiresAtUtc и user. Передавайте токен как Authorization: Bearer <accessToken>. В Swagger нажмите Authorize и вставьте только значение токена. Срок действия — 30 минут, затем войдите повторно. Смена ключа делает ранее выданные токены недействительными.

## Модель и правила

- Book — физический экземпляр с уникальным InventoryNumber. Одинаковые названия с разными номерами допустимы.
- Reader — читатель с уникальным CardNumber, контактами и блокировкой. Это не учётная запись пользователя API.
- Loan — выдача Book конкретному Reader со сроком и моментом возврата.

По умолчанию срок выдачи — 14 дней, лимит — 5 активных выдач читателю. Архивный или уже выданный экземпляр нельзя выдать. Заблокированный читатель не получает новые книги, но может вернуть имеющиеся. В БД есть частичный уникальный индекс активной выдачи экземпляра; выдача и проверка лимита выполняются в транзакции.

Book/Reader с историей нельзя удалить: используйте архивирование/блокировку. Активную Loan нельзя удалить. Завершённую Loan администратор может удалить как учебную операцию исправления истории; это влияет на отчёты. Инвентарный номер и номер билета после создания неизменяемы. PUT выдачи меняет срок, а книга и читатель остаются прежними.

## API и права

Все маршруты начинаются с /api/v1. Полная спецификация: /swagger/v1/swagger.json при включённом Swagger.

| Ресурс | Методы | Права |
|---|---|---|
| /auth/login | POST | Все |
| /auth/me | GET | User, Admin |
| /books и /books/{id} | GET | Все |
| /readers и /readers/{id} | GET | User, Admin |
| /loans и /loans/{id} | GET | User, Admin |
| /books, /readers, /loans | POST | Admin |
| /books/{id}, /readers/{id}, /loans/{id} | PUT, DELETE | Admin |
| /books/{id}/archive, /books/{id}/restore | POST | Admin |
| /readers/{id}/block, /readers/{id}/unblock | POST | Admin |
| /loans/{id}/return | POST | Admin |
| /reports/active-loans | GET | User, Admin |
| /reports/overdue-loans | GET | User, Admin |
| /reports/popular-books | GET | User, Admin |
| /reports/reader-statistics | GET | User, Admin |

Примеры запросов:

```text
GET /api/v1/books?search=мастер&availableOnly=true&page=1&pageSize=20
GET /api/v1/readers?search=Иванова
GET /api/v1/loans?readerId=1&activeOnly=true
GET /api/v1/reports/popular-books?from=2026-01-01T00:00:00Z&to=2027-01-01T00:00:00Z
```

Пагинация: page от 1, pageSize от 1 до 100, по умолчанию 20. Оболочка списков: { items, totalCount, page, pageSize }. Все даты — ISO 8601, UTC; при вводе допустимо явное смещение. В фильтрах периода from включительно, to исключительно; фильтруется момент выдачи. Статистика активных/просроченных отражает состояние сейчас среди выдач выбранного периода. Рейтинг относится к экземплярам, а не ко всем изданиям одного произведения. Поиск по названию/автору/номеру учитывает русский регистр. genre — точное совпадение строки.

Коды: 200 чтение/изменение, 201 создание с Location, 204 удаление, 400 валидация, 401 вход отсутствует/недействителен, 403 недостаточно прав, 404 запись отсутствует, 409 конфликт состояния/уникальности. Ошибки используют ProblemDetails с traceId; неизвестные поля входного JSON отклоняются.

## Postman

Импортируйте оба файла из postman/ и выберите окружение Library API — localhost. При необходимости поменяйте baseUrl. Запускайте коллекцию полностью, сверху вниз через Collection Runner. Она сама получает оба токена, запоминает ID, создаёт собственные записи и очищает их в конце. Демонстрационные данные не изменяются.

Для проверки лимита используется начальный читатель R-004 с пятью активными выдачами. Если вручную изменить его выдачи, этот конкретный тест перестанет соответствовать исходному набору. Остальные тестовые записи получают уникальные номера. После прерванного прогона они могут остаться в БД, но следующий прогон использует новые номера.

CLI при наличии Node.js и Newman:

```powershell
npx newman@6.2.1 run .\postman\LibraryApi.postman_collection.json -e .\postman\LibraryApi.local.postman_environment.json
```

## Проверка и сборка

```powershell
dotnet test LibraryApi.sln -c Release
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

publish.ps1 создаёт artifacts/LibraryApi-1.0.0-win-x64.zip и SHA256. При существующем каталоге версии останавливается, чтобы не смешивать старые и новые файлы. Перед публикацией новой версии задайте -Version 1.0.1. Автоматические тесты используют отдельные временные SQLite-БД, не затрагивая локальную библиотеку.

## Настройки и клиентская лабораторная

Jwt:Key обязателен. Источники: appsettings.json, user-secrets в Development, локальный demo-settings.json, переменные окружения, аргументы CLI. Переменные окружения и CLI имеют высший приоритет. Для другого файла настроек задайте LIBRARY_SETTINGS_FILE. Строка подключения — ConnectionStrings__Library; лимит — Library__ActiveLoanLimit; срок — Library__DefaultLoanDays. Demo__Seed и Demo__Swagger включают учебный набор и Swagger явно даже при Production.

Учебные сценарии используют loopback HTTP. При размещении на внешнем сервере настройте HTTPS и отдельные учётные данные. Открытая регистрация, refresh-токены и личный кабинет читателя в объём этой лабораторной не входят.

Будущий клиент использует DTO, JWT и /api/v1. Для браузерного клиента укажите разрешённые адреса в Cors:Origins, например ["http://localhost:5173"]. Для настольного клиента CORS не требуется. User — просмотр сотрудником, Admin — управление; роль User не означает доступ только к собственному Reader.

## Материалы защиты и критерии

- [Подробное объяснение реализации](docs/DEFENSE_GUIDE.md).
- [Сценарий демонстрации](docs/DEMO_SCENARIO.md).
- [Исходный план](docs/LAB_PLAN.md).
- [Результаты проверки](docs/VERIFICATION.md).

Обязательные 10 баллов: три класса с методами, CRUD, четыре объединяющих отчёта, LINQ Where и Select. Дополнительно: JWT и роли (+2), Release и Postman (+2), опубликованный GitHub Release (+1). Факт публикации и окончательные результаты указаны в VERIFICATION.md.
