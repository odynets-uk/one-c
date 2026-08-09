# OneC

OneC — CLI-синхронізатор даних з 1С (через COM) у SQLite + API для читання даних стороннім сервісам.

## Стек і стан проєкту

- .NET 10, C#.
- COM-з'єднання з 1С через `comcntr.dll` (x32, `V83.COMConnector`).
- SQLite через `Microsoft.Data.Sqlite`.
- Serilog: Console + File (rolling) + SQLite (для `sync_log`).
- CLI: `OneC.Cli` (x86, Prefer32Bit).
- API: `OneC.Api` (ASP.NET Core).

Архітектурні правила, межі шарів, DI, тестування та процес внесення змін визначені в [`AGENTS.md`](AGENTS.md). README не дублює ці правила.

## Структура

```text
OneC.Cli/             Presentation/CLI: команди, Program.cs, appsettings
OneC.Application/     Use cases, DTOs, ports/interfaces
OneC.Domain/          Сутності, value objects, metadata, profiles
OneC.Infrastructure/  COM, XSD-парсер, SQLite, репозиторії, Serilog
OneC.Api/             Presentation/API: controllers, Program.cs
.agents/tasks/        Задачі: index.md, gantt.md, task-{NNN}-{slug}.md
.agents/templates/    Шаблони: task-template.md, entity-design-template.md, profile-design.md
OneC.slnx             Solution
```

## Конфігурація

### `OneC.Cli/appsettings.json`

```json
{
  "ActiveConnection": "Kplus",
  "Connections": {
    "Kplus": {
      "ConnectionStringName": "Kplus",
      "Schemas": {
        "Catalog": { "Profile": "catalog", "RootType": "CatalogObject.Номенклатура" },
        "Products": { "Profile": "products", "RootType": "CatalogObject.Номенклатура" },
        "Expenses": { "Profile": "expenses", "RootType": "DocumentObject.РеализацияТоваровУслуг" }
      }
    }
  },
  "SingleInstance": { "Enabled": true, "GracefulShutdownTimeoutSeconds": 10 },
  "ComRegistration": {
    "EnsureRegisteredOnStartup": true,
    "Regsvr32Path": "C:\\Windows\\SysWOW64\\regsvr32.exe",
    "LibraryPath": "K:\\Portables\\1cv8\\8.3.10.2252\\bin\\comcntr.dll"
  }
}
```

### `OneC.Cli/appsettings.local.json` (в .gitignore)

```json
{
  "ConnectionStrings": {
    "Kplus": "File=\"K:\\Portables\\1cv8\\1c-com-module\\1CConector\\KplusBase\";Usr=\"Менеджер2\";Pwd=\"<encrypted>\";"
  }
}
```

Пароль зберігається в зашифрованому вигляді (AES, ключ у коді). `appsettings.local.json` не комітиться.

## Запуск

Потрібні .NET 10 SDK та зареєстрована `comcntr.dll` (x32).

```powershell
dotnet restore OneC.slnx
dotnet build OneC.slnx -c Release
dotnet run --project OneC.Cli -- test-connection
```

## Команди CLI

| Команда | Призначення |
|---|---|
| `test-connection` | Підключення до бази 1С, вивід версій платформи та конфігурації |
| `list-catalogs` | Список довідників з XSD-схеми |
| `list-enums` | Переліки та їх значення з XSD-схеми |
| `list-profiles` | Список доступних профілів вибірки |
| `get-catalog <name> --profile <file> [--mode full\|incremental] [--batch-size N]` | Вичитування даних згідно з профілем (JSON) |
| `sync --profile <profile> [--mode full\|incremental] [--since 1d\|24h\|1w]` | Синхронізація даних у SQLite |

## База даних

SQLite — основне сховище. Таблиці: `items`, `prices`, `stock`, `sync_log`. Схема створюється через `SchemaInitializer` при старті.

## Де шукати правила

- [`AGENTS.md`](AGENTS.md) — архітектура, залежності шарів, DI, БД, інтеграції, тестування, git-workflow і task workflow.
- [`.agents/templates/entity-design-template.md`](.agents/templates/entity-design-template.md) — шаблон опису сутності/профілю перед реалізацією.
- [`.agents/templates/profile-design.md`](.agents/templates/profile-design.md) — авторитетний посібник з формату профілів вибірки (поля, фільтри, валідація, команди, тестування).
- [`.agents/tasks/`](.agents/tasks/) — індекс задач, Gantt, task-файли.
