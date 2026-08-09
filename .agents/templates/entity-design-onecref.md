# Entity Design — OneCRef (Value Object)

> Заповнений шаблон для Value Object `OneCRef` (1С-посилання). Вхідний контракт для реалізації Domain, Application, Infrastructure і тестів.

## 1. Metadata

- Entity/Profile name: `OneCRef`
- Source 1C type: `CatalogRef.*` (Ref-поля довідників, напр. `Parent`, `Ref`)
- Root 1C type: `CatalogRef.Номенклатура`
- Related entities: `categories`, `items`, `prices`, `stock`
- Publicly exposed: `yes` (як GUID string у JSON/API)
- Профіль: `categories` (та інші профілі з Ref-полями)

## 2. Purpose

### Business purpose

Інкапсулює логіку роботи з 1С-посиланням (Ref): парсинг GUID, нормалізація порожнього посилання (нульовий GUID) до `null`, валідація некоректних значень.

### Lifecycle

- Created when: парсинг сирого рядка з COM (`OneCRef.FromString`)
- Updated when: immutable (record struct)
- Deactivated/archived when: n/a
- Deleted when: n/a
- Immutable fields: `Value`

## 3. SQLite schema

> `OneCRef` — Value Object, не таблиця. Мапиться на колонку `TEXT`.

```sql
-- OneCRef → TEXT (GUID string), NULL для порожнього посилання
-- id TEXT PRIMARY KEY
-- parent_id TEXT NULL
```

### Column semantics

| Column | Required | SQLite type | Default | Meaning | Mutable | Sensitive |
|---|---:|---|---|---|---:|---:|
| `id` | yes | `TEXT` | — | GUID посилання | no | no |
| `parent_id` | no | `TEXT` | `NULL` | GUID батьківського посилання | yes | no |

## 4. Indexes and uniqueness

```sql
-- PRIMARY KEY (id)
-- CREATE INDEX categories_parent_id_index ON categories(parent_id)
```

| Index | Columns/expression | Unique | Partial condition | Reason |
|---|---:|---|---|---|
| `categories_parent_id_index` | `parent_id` | no | — | швидкий пошук дітей |

## 5. Relationships

| Related entity | Relation | Required | FK owner | Delete behavior | Additional rules |
|---|---:|---|---|---|---|
| `categories` | self-referencing (parent) | no | `categories.parent_id` | restrict | порожнє посилання = null |

## 6. Value Objects

| Value Object | Backing fields | Invariants | Normalization | SQL representation | API representation |
|---|---|---|---|---|---|
| `OneCRef` | `Guid Value` | валідний GUID; нульовий GUID = порожнє посилання | `00000000-0000-0000-0000-000000000000` → `null` | `TEXT` (GUID string) | `string` (GUID) або `null` |

## 7. Domain rules and invariants

### Valid states

- `OneCRef` з валідним, ненульовим GUID
- `null` (порожнє посилання 1С)

### Invalid states

- Невалідний GUID (напр. `"Бухгалтерські бланки"` — якщо туди потрапив Description)

### Create rules

- `OneCRef.FromString(null)` → `null`
- `OneCRef.FromString("00000000-0000-0000-0000-000000000000")` → `null`
- `OneCRef.FromString(validGuid)` → `OneCRef`
- `OneCRef.FromString(invalid)` → `InvalidOperationException`

### Update rules

- Immutable — не оновлюється

### Delete/archive rules

- n/a

### Cross-entity rules

- Порожнє посилання (null) не створює FK-зв'язок

## 8. Validation

| Field/rule | Validation | Layer | Error code/message |
|---|---|---|---|
| `Value` | `Guid.TryParse` | Domain | `InvalidOperationException: Invalid 1C reference value '{raw}': expected a GUID` |

## 9. Application contract

### Commands

| Command | Input | Preconditions | Result | Errors |
|---|---|---|---|---|
| n/a (Value Object, не use case) | — | — | — | — |

### Queries

| Query | Input | Result/read model | Filtering/sorting/paging |
|---|---|---|---|
| n/a | — | — | — |

### Ports

```text
n/a — OneCRef не потребує портів; використовується всередині CatalogReader/ComValueMapper.
```

## 10. API contract

| Method | Route | Authorization | Request | Response | Errors |
|---|---|---|---|---|---|
| n/a (не API-сутність) | — | — | — | — | — |

## 11. 1C mapping (COM→SQLite)

```text
Ref-поле 1С → COM-об'єкт → УникальныйИдентификатор()/Ref → GUID string → OneCRef.FromString
Порожнє посилання (нульовий GUID) → null
```

### Mapping decisions

- COM value mapper strategy: `OneCRef.FromString` у `CatalogReader.GetRefId`
- Ref handling: GUID string через `OneCRef`
- Tabular sections: n/a
- Incremental update field: n/a

## 12. Migration and existing data

- Existing rows affected: `no`
- Backfill required: `no`
- Data-loss risk: `none`

## 13. Tests and acceptance criteria

### Domain tests

- `OneCRefTests`: валідний GUID, нульовий GUID → null, null/порожній → null, невалідний → throw

### Application tests

- n/a (не use case)

### Infrastructure tests

- `CatalogReader` використовує `OneCRef` для Ref/Parent

### Acceptance criteria

- [x] TC1: `OneCRef.FromString` коректно парсить GUID.
- [x] TC2: Порожнє посилання (нульовий GUID) → null.
- [x] TC3: Невалідне значення → `InvalidOperationException`.
- [x] TC4: Тести проходять (`dotnet test`).

## 14. Implementation output

- [x] Domain Value Object `OneCRef` (`OneC.Domain/ValueObjects/OneCRef.cs`)
- [x] Рефакторинг `CatalogReader.GetRefId` на `OneCRef`
- [x] Unit tests `OneCRefTests`

## 15. Open decisions

- `none`