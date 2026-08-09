# Design профілів вибірки OneC

> Авторитетний посібник з формату профілів вибірки даних з 1С. Профілі описують: які поля брати, фільтри, зв'язки (залишки, ціни), режим (повний дамп / інкрементальний).

## 1. Призначення

Профіль вибірки — JSON-файл, що описує **як** вичитувати дані з 1С через COM. Кожен профіль відповідає одній сутності (напр. `categories`, `products`, `expenses`).

Файли профілів знаходяться в `profiles/` (напр. `profiles/categories.json`, `profiles/products.json`).

## 2. Команди CLI

| Команда | Опис |
|---|---|
| `get-catalog <name> --profile <file>` | Вичитати дані каталогу згідно з профілем, вивести у JSON |
| `--mode full\|incremental` | Режим вибірки (за замовчуванням `full`) |
| `--batch-size N` | Розмір батча. `-1` = всі дані (за замовчуванням для каталогу) |
| `sync --profile <file> [--mode ...] [--since ...]` | Синхронізація даних у SQLite |

Приклад:
```powershell
dotnet run --project OneC.Cli -- get-catalog categories --profile profiles/categories.json --batch-size 5
```

## 3. Структура профілю

```json
{
  "name": "products",
  "source": { "type": "CatalogObject.Номенклатура", "schema": "./data-enterprise.xsd" },
  "mode": "full",
  "batch_size": -1,
  "filters": { ... },
  "skip_items_without_prices": false,
  "skip_items_without_stock": false,
  "output": { ... },
  "table": "products",
  "columns": [ ... ],
  "references": [ ... ],
  "indexes": [ ... ]
}
```

## 4. Поля (обов'язкові / необов'язкові)

### Обов'язкові

| Поле | Опис |
|---|---|
| `name` | Назва профілю (якщо відсутнє — береться з імені файлу) |
| `source.type` | Root 1C-тип (напр. `CatalogObject.Номенклатура`) |
| `table` | Цільова таблиця SQLite |
| `columns[]` | Список колонок (мінімум одна, обов'язково з `id`) |

### Необов'язкові

| Поле | Опис |
|---|---|
| `mode` | `full` (за замовчуванням) або `incremental` |
| `batch_size` | Розмір батча, `-1` = всі дані (за замовчуванням) |
| `filters` | Фільтри (див. нижче) |
| `output` | Вихід: JSON-файл, SQLite БД |
| `references` | Foreign keys для SQLite |
| `indexes` | Індекси для SQLite |
| `skip_items_without_prices` | `false` (за замовчуванням) = товар виводиться з `prices: null`; `true` = пропустити товар |
| `skip_items_without_stock` | аналогічно для `stock` |

## 5. Колонки (`columns[]`)

```json
{
  "source": "Ref",
  "name": "id",
  "sql_type": "TEXT PRIMARY KEY",
  "transform": "NOT {value}",
  "validation": { ... }
}
```

| Поле | Обов'язкове | Опис |
|---|---|---|
| `source` | так | Ім'я поля 1C (напр. `Ref`, `Code`, `Description`, `Parent`, `Комментарий`, `DeletionMark`) |
| `name` | так | Цільова назва колонки (напр. `id`, `legacy_id`, `name`, `parent_id`, `is_active`) |
| `sql_type` | так | SQLite-тип (напр. `TEXT PRIMARY KEY`, `INTEGER NOT NULL DEFAULT 1`, `JSONB`) |
| `transform` | ні | Трансформація значення. Підтримується `"NOT {value}"` (інверсія boolean) |
| `validation` | ні | Правила валідації (див. нижче) |

### validation

| Поле | Опис |
|---|---|
| `vo` | Ім'я доменного Value Object для валідації. Підтримується `"OneCRef"` (1C-посилання GUID). Замінює `required`/`regex` для GUID-полів |
| `exists` | Runtime-валідація існування: `"table.column"` (напр. `"categories.id"`). Значення або null, або має існувати в зазначеній таблиці. Використовується для зовнішніх посилань (напр. `category_id` → `categories.id`) |
| `empty_to_null` | Порожні масиви/об'єкти (`[]`/`{}`) → `null`. **Скалярні значення** (int, float, string, bool) не чіпаються. Тільки для `sql_type` JSON/JSONB |
| `required` | Значення обов'язкове |
| `nullable` | Дозволено null |
| `unique` | Значення має бути унікальним |
| `regex` | Regex-патерн, якому має відповідати значення |
| `min_length` / `max_length` | Діапазон довжини рядка |
| `case` | Регістр для regex (`insensitive` або null) |
| `boolean` | `strict` — значення має бути boolean |

### Приклад GUID-колонки через `vo`

```json
{ "source": "Ref", "name": "id", "sql_type": "TEXT PRIMARY KEY",
  "validation": { "vo": "OneCRef" } },
{ "source": "Parent", "name": "category_id", "sql_type": "TEXT",
  "validation": { "vo": "OneCRef", "exists": "categories.id" } }
```

`OneCRef.FromString` покриває: `null`/порожній → null, нульовий GUID `00000000-...` → null, валідний GUID → GUID string, невалідне значeння → `InvalidOperationException`.

## 6. Фільтри (`filters`)

```json
"filters": {
  "field_filters": { "IsFolder": true, "Code": ["000002841", "000000384"] },
  "prices": {
    "price_types": ["Цена продажи", "Цена закупочная"],
    "exclude_zero_price": true,
    "changed_since": "45d"
  },
  "stock": {
    "warehouses": ["*"],
    "only_positive": false,
    "changed_since": "2026-07-01:2026-07-31"
  }
}
```

### `field_filters`

Проста фільтрація полів каталогу:
- **Скаляр** (`"IsFolder": true`) → `=` рівність
- **Масив** (`"Code": ["...", "..."]`) → `IN (...)`
- **`Description`** → `LIKE '%...%'` (частковий збіг, "міститься")

### `filters.prices` (ціни — масив об'єктів)

| Поле | Опис |
|---|---|
| `price_types` | Список типів цін або `["*"]` (всі). Фільтрує `price_type` |
| `exclude_zero_price` | Виключити ціни з `price == 0.0` |
| `changed_since` | Період змін. Відносний: `"14d"`, `"2w"`, `"6h"` (суфікси `s/m/h/d/w`) або абсолютний діапазон `"2026-07-01:2026-07-31"`. Фільтрує `timestamp` |

### `filters.stock` (залишки — масив об'єктів)

| Поле | Опис |
|---|---|
| `warehouses` | Список складів або `["*"]` (всі). Фільтрує `warehouse` |
| `only_positive` | Тільки позитивні залишки (`quantity > 0`) |
| `changed_since` | Період змін (див. вище). Фільтрує `last_movement` |

### `filters.items` (для product-профілів)

| Поле | Опис |
|---|---|
| `codes` | Список кодів товарів |
| `artikuls` | Список артикулів |
| `guids` | Список GUID |
| `name_contains` | Підрядок у назві |

## 7. Ціни та кількості — `decimal(15,4)` як integer у SQLite

У SQLite фінансові значення зберігаються як **INTEGER** (множаться на 10000, щоб зберегти 4 знаки після коми). У presentation шарі (JSON/API) діляться назад на 10000.

- Точність: 4 знаки після коми
- Максимум: 15 значущих цифр
- Поля: `price`, `base_cost`, `markup_pct`, `quantity`

## 8. `sql_type` JSON / JSONB

- `prices` і `stock` — масиви об'єктів, зберігаються як `JSONB` або `JSON`
- SQLite зберігає JSONB як `BLOB` (внутрішній формат, з 3.45.0)
- **Індекси для пошуку по масивах не створюються** — пошук/індексація виконується в PostgreSQL через синхронізацію
- Порожні масиви нормалізуються в `null` через `validation.empty_to_null`

## 9. Вихід (`output`)

```json
"output": {
  "json": { "file_path": "export/products.json", "pretty": true },
  "db": { "file_path": "export/kplus.db", "engine": "sqlite", "version": "3.37+" }
}
```

- `json.file_path` — вихідний JSON-файл (може містити `{date}` placeholder)
- `json.pretty` — pretty-print
- `db` — для регулярної синхронізації в SQLite (використовується командою `sync`)

## 10. Профілювання

Після кожного витягу `GetCatalogService` логує через Serilog:
- Час виконання (ms)
- CPU (TotalProcessorTime, ms)
- RAM (WorkingSet64 delta, MB)

## 11. Як тестувати

1. **Unit tests**: `dotnet test OneC.slnx` — перевіряє валідацію профілів (`ProfileLoaderTests`), маппінг (`ComValueMapperTests`), Value Objects (`OneCRefTests`).
2. **Тестовий витяг** (з живим COM): `get-catalog <name> --profile profiles/<file>.json --batch-size 5` — перевірити невелику вибірку.
3. **Повний витяг**: `get-catalog <name> --profile profiles/<file>.json`.
4. **Звірка з базою**: перевірити GUID, ціни, залишки з реальними даними 1С.

## 12. Існуючі профілі

| Файл | Сутність | Особливості |
|---|---|---|
| `profiles/categories.json` | Категорії (папки Номенклатури) | `IsFolder: true`, `vo: "OneCRef"` для id/parent_id |
| `profiles/products.json` | Товари | `IsFolder: false`, `exists: "categories.id"`, фільтри prices/stock, `empty_to_null` |

## 13. Open decisions / TODO

- Профілювання розміру батча (100/500/1000) — вимірювання часу/пам'яті