# Задача: Динамічний читач даних

| | |
|---|---|
| **ID** | `task-004-catalog-reader` |
| **Статус** | `in-progress` |
| **Залежності** | `task-003-profiles` |
| **Оцінка** | 4d |
| **Пріоритет** | high |

## Мета

Реалізувати динамічне вичитування даних з 1С через COM з пакетною обробкою, маппінгом значень та трансформаціями відповідно до узгодженого формату профілів.

## Обсяг

- [ ] `OneC.Domain/Profiles/` — розширити моделі профілю:
  - `source.type` (root type 1С)
  - `columns[]` з `sql_type` + `validation` (розділено)
  - `transform` (напр. `"NOT {value}"` для DeletionMark → is_active)
  - структуровані `references`/`indexes`
- [ ] `OneC.Infrastructure/Readers/ComValueMapper.cs` — маппінг COM-значень → .NET (Ref→GUID, Enum→string, decimal, bool, dateTime) + підтримка `transform`
- [ ] `OneC.Infrastructure/Readers/CatalogReader.cs` — динамічне вичитування: `Выбрать()` + `ВыбратьСледующий()`, пакетна обробка з `batch_size`
- [ ] `OneC.Infrastructure/Profiles/ProfileLoader.cs` — розширена валідація JSON-профілів (в коді, без JSON Schema)
- [ ] `OneC.Cli` команда `get-catalog <name> --profile <profile> [--mode full|incremental] [--batch-size N]`
  - Для каталогу за замовчуванням: `--mode full --batch-size -1` (всі дані)
- [ ] Профілювання розміру батча (100/500/1000) — вимірювання часу/пам'яті

## Критерії готовності

- [ ] `get-catalog` вичитує дані з бази 1С і виводить у JSON згідно з профілем
- [ ] Маппінг типів коректний (Ref→GUID, Enum→string, decimal, bool, dateTime)
- [ ] `transform` працює (напр. `"NOT {value}"` для DeletionMark)
- [ ] Пакетна обробка працює з `batch_size` (використовується ComValueMapper + CatalogReader)
- [ ] Валідація профілів в `ProfileLoader` з логуванням помилок
- [ ] Тести маппінгу та валідації (unit) проходять
- [ ] Коміт створено з multiline повідомленням

## Узгоджені рішення (brainstorming 2026-08-09)

| Питання | Рішення |
|---|---|
| `transform` | Підтримуємо трансформації значень (напр. `"NOT {value}"` для DeletionMark → is_active) |
| `mode`/`batch_size` | Параметри CLI: `--mode full` / `--batch-size 500`. Для каталогу `--batch-size -1` = всі дані |
| Валідація профілів | В коді (`ProfileLoader`) + логування помилок. JSON Schema не потрібна |
| `Code` regex | Залишаємо `^\d{9}$` (9 цифр) — при невідповідності помилка валідації |

### Приклад формату профілю (узгоджений)

```json
{
  "name": "categories",
  "source": { "type": "CatalogObject.Номенклатура", "schema": "./data-enterprise.xsd" },
  "mode": "full",
  "batch_size": -1,
  "filters": { "field_filters": { "IsFolder": true } },
  "output": {
    "json": { "file_path": "export/categories.json", "pretty": true },
    "db": { "file_path": "export/kplus.db", "engine": "sqlite", "version": "3.37+" }
  },
  "table": "categories",
  "columns": [
    { "source": "Ref", "name": "id", "sql_type": "TEXT PRIMARY KEY",
      "validation": { "required": true, "regex": "^[a-f0-9-]{36}$", "case": "insensitive" } },
    { "source": "DeletionMark", "name": "is_active", "sql_type": "INTEGER NOT NULL DEFAULT 1",
      "transform": "NOT {value}", "validation": { "boolean": "strict" } }
  ],
  "references": [
    { "column": "parent_id", "references": "categories(id)", "on_delete": "RESTRICT", "on_update": "NO ACTION" }
  ],
  "indexes": [
    { "name": "categories_parent_id_index", "columns": ["parent_id"] }
  ]
}
```

## Технічні рішення / Контекст

### Методика роботи з 1С COM (вивчено з попереднього проекту `1c_ex`)

**Ключове відкриття:** Кириличні методи COM-об'єкта 1С (`Выбрать()`, `Следующий()`, `Справочники`) **не працюють через dynamic binding**. Попередній проект використовує **`Query`** (запит) — всі методи латиницею:

```csharp
// 1. Створюємо запит (латиниця — працює через dynamic)
var query = _v8.NewObject("Query");

// 2. Текст запиту — кирилиця тільки в рядку (ок)
query.Text = "SELECT ... FROM Справочник.Номенклатура AS Номенклатура ORDER BY ...";

// 3. Виконання (латиниця)
var queryResult = query.Execute();
var selection = queryResult.Choose();

// 4. Ітерація (латиниця)
while (selection.Next()) { ... }
```

**Доступ до полів** — через `InvokeMember` (не через dynamic):
```csharp
var type = ((object)obj).GetType();
return type.InvokeMember(propertyName, BindingFlags.GetProperty, null, obj, null);
```

**Конвертація значень** — через `_v8.String(value)` (для GUID, Ref тощо).

### План переписування `CatalogReader`

1. **Переписати `CatalogReader`** на методику `Query`:
   - `NewObject("Query")` → `query.Text = "SELECT ... FROM Справочник.{catalogName} ..."`
   - `query.Execute()` → `queryResult.Choose()` → `selection.Next()`
   - Доступ до полів через `InvokeMember` (кирилиця працює)
   - `_v8.String(value)` для конвертації Ref/GUID
2. **Фільтр `IsFolder=true`** — додати в `WHERE` запиту: `WHERE Номенклатура.IsFolder = TRUE`
3. **Маппінг полів** — через `InvokeMember` згідно з профілем
4. **Тестовий витяг** — запустити `get-catalog --profile profiles/categories.json --batch-size 5`

### Інші рішення

- `batch_size: -1` — вичитати всі записи без пакетування
- Табличні частини — окремі записи або JSON
- Маппінг: Ref → GUID string, Enum → string, decimal → decimal, bool → bool, dateTime → DateTime

## Action log

| Дата | Дія | Результат |
|---|---|---|
| 2026-08-08 | Задача створена | backlog |
| 2026-08-09 | Узгоджено формат профілю (brainstorming): transform, mode/batch_size як CLI параметри, валідація в коді, Code regex 9 цифр | in-progress |
| 2026-08-09 | Вивчено методику з `1c_ex`: Query замість Выбрать(), InvokeMember для полів, _v8.String для конвертації | in-progress |
| 2026-08-09 | Переписано CatalogReader на Query, тестовий витяг категорій працює (5 записів, GUID коректні) | in-progress |
| 2026-08-09 | Виправлено нормалізацію вихідного JSON: порожні 1С-значення (Неопределено/NULL) → null замість {}; кирилиця без \uXXXX (Encoder=UnsafeRelaxedJsonEscaping). Додано IComSession + ComValueMapperTests (31 тестів проходять) | in-progress |
