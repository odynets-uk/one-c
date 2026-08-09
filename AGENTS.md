# AGENTS.md — правила розробки OneC

Цей файл є авторитетним джерелом архітектурних, DI, persistence, testing і change-management правил. Опис продукту, endpoint-ів і quick start знаходиться в [`README.md`](README.md); не дублюй його тут.

Перед реалізацією нової або перебудовою наявної сутності використовуй [`AGENTS/ENTITY-DESIGN-TEMPLATE.md`](.agents/templates/entity-design-template.md). Заповнений шаблон є вхідним контрактом для Domain, Application, Infrastructure, Presentation і тестів.

## Архітектура

OneC — CLI-синхронізатор даних з 1С (через COM) у SQLite + API для читання даних стороннім сервісам. Solution містить чотири проєкти:

```text
OneC.Cli (Presentation/CLI)
  ├── OneC.Application
  └── OneC.Infrastructure
        └── OneC.Application
              └── OneC.Domain
OneC.Api (Presentation/API)
  ├── OneC.Application
  └── OneC.Infrastructure
        └── OneC.Application
              └── OneC.Domain
```

- `OneC.Domain` не має project references; містить сутності, value objects, aggregates, domain events та exceptions, коли вони реалізовані.
- `OneC.Application` містить use cases, DTOs і ports/interfaces.
- `OneC.Infrastructure` містить COM-з'єднання, XSD-парсер, SQLite, репозиторії та інші adapters і реалізує application ports.
- `OneC.Cli` містить команди CLI та `Program.cs`; він є єдиним місцем виклику Infrastructure DI.
- `OneC.Api` містить controllers та `Program.cs`; він є єдиним місцем виклику Infrastructure DI для API.
- Залежності спрямовані всередину: Presentation → Application/Infrastructure, Infrastructure → Application, Application → Domain.
- Бізнес-логіка не розміщується в Presentation або Infrastructure adapters. Application оркеструє use cases, domain model захищає інваріанти.
- Aggregate roots мають захищати інваріанти; для нових доменних моделей використовуй constructors/factory methods, immutable value objects і не допускай неконтрольованого mutable state.

## Конвенції

- C#/.NET 10; типи й методи — PascalCase, параметри та locals — camelCase.
- Кожен шар — окремий project у `OneC.slnx`.
- Application оголошує тільки ports/interfaces; concrete implementations реєструє Infrastructure через `AddXxx` extension methods.
- Не витягуй concrete Infrastructure types у Application.
- Domain не залежить від EF, ASP.NET, COM, SQLite чи інших infrastructure concerns.

## DI і persistence

У Infrastructure використовуй статичні extension methods на кшталт:

```csharp
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services)
```

Викликай Infrastructure registration тільки з `OneC.Cli/Program.cs` або `OneC.Api/Program.cs` (presentation-level composition root).

SQLite — основне сховище даних. Таблиці: `items`, `prices`, `stock`, `sync_log`. Схема створюється через `SchemaInitializer` при старті.

## Інтеграції

### 1C (COM)

- Connector-specific code розміщуй у `OneC.Infrastructure/Com`.
- Обгортка над `V83.COMConnector` (dynamic, IDispatch) — `ComConnector`.
- Життєвий цикл: `ComSession` — Connect/Dispose, обробка HRESULT, логування.
- Пароль шифрується AES з фіксованим ключем у коді (`ConnectionStringProtector`).
- CLI має бути x86 (Prefer32Bit) через 32-бітну `comcntr.dll`.
- При відсутності з'єднання — логувати зрозумілу помилку (Serilog), не падати з незрозумілим exception.

### XSD-схема

- `data-enterprise.xsd` (~1.9 МБ) — повна схема бази 1С, в корені проекту (в .gitignore).
- Парсинг потоковий (XmlReader), не завантажувати весь файл у пам'ять.
- Моделі метаданих — в `OneC.Domain/Metadata`.
- Кешування результату в JSON для швидкого повторного запуску.

### Профілі вибірки

- Абстрактні моделі профілів — в `OneC.Domain/Profiles`.
- Конкретні профілі (`profiles/categories.json`, `profiles/products.json`) — описані в [`profile-design.md`](.agents/templates/profile-design.md).
- Профілі описують: які поля брати, фільтри (IsFolder, Code, prices, stock), зв'язки (залишки, ціни), режим (повний дамп / інкрементальний).
- Для інформації про формат профілю дивись [`profile-design.md`](.agents/templates/profile-design.md) — авторитетний посібник.

## Логування

- Serilog: Console + File (rolling) + SQLite (для `sync_log`).
- Рівні: `Information` (успішні операції), `Warning` (не критичні проблеми), `Error` (помилки COM, з'єднання, валідації), `Fatal` (критичні).
- Structured logging: використовуй іменовані параметри в шаблонах повідомлень.

## Тестування

- Domain: xUnit/NUnit pure unit tests, без DI та БД.
- Application: handler tests з in-memory ports/test doubles.
- Infrastructure: integration tests з локальною тестовою БД (SQLite in-memory).
- COM-залежний код: тестувати парсер XSD, JSON-профілі, маппінг значень (без реального COM).

Нові доменні правила обов'язково покривай unit tests. Нові adapters — integration tests.

## Security

- Пароль у `appsettings.local.json` зберігається в зашифрованому вигляді (AES, ключ у коді).
- `appsettings.local.json` — в .gitignore, не комітиться.
- Не коміть connection strings, паролі чи інші secrets.

## Git-workflow

- **Після кожної завершеної під-задачі** — створюється коміт (обов'язково).
- **Multiline commit message** — що було зроблено, наприклад:

```
feat(com): add COM connector and test-connection command

- Implement ComConnector wrapper over V83.COMConnector (dynamic, IDispatch)
- Implement ComSession lifecycle: Connect/Dispose, HRESULT handling
- Implement AES ConnectionStringProtector for password encryption
- Add test-connection command to OneC.Cli
- Add x86 (Prefer32Bit) configuration to OneC.Cli.csproj
- Add Serilog console + file logging
```

- **Одна логічна зміна = один коміт.** Не змішувати різні задачі в один коміт.
- **Тести** — після кожної значущої зміни запускати `dotnet test`.

## Tasks і планування

Task-файли знаходяться в `.agents/tasks/`:

- `index.md` — індекс задач (статуси, залежності, оцінки).
- `gantt.md` — Gantt-діаграма залежностей (Mermaid).
- `task-{NNN}-{slug}.md` — task-файл, який комітиться в репозиторій.
- Шаблони — в `.agents/templates/`.

Нові tasks мають містити унікальний id, status, depends_on і, коли відома, оцінку. Оновлюй `_agent` action_log під час роботи та архівуй завершені tasks відповідно до шаблону.

Пріоритети: security/auth/input-validation спочатку; далі architectural debt, dependency-driven critical path і quick wins, що розблоковують інші tasks.

## Change checklist

- Перевір залежності шарів і відсутність business logic в adapters/Presentation.
- Додай unit tests для нових domain rules.
- Додай integration tests для нових adapters.
- Онови OpenAPI contract, якщо змінено API.
- Проведи security review для нової surface area.
- Онови task-файли, `index.md` і `gantt.md`, якщо змінено task metadata.
- Створи коміт з multiline повідомленням.