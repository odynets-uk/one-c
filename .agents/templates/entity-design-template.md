# Entity Design Template

> Заповни цей файл для однієї сутності або профілю вибірки. Після заповнення файл є вхідним контрактом для реалізації Domain, Application, Infrastructure і тестів.

## 1. Metadata

- Entity/Profile name: `TODO`
- Source 1C type: `TODO` (наприклад, `CatalogObject.Номенклатура`)
- Root 1C type: `TODO` (наприклад, `CatalogRef.Номенклатура`)
- Related entities: `TODO`
- Publicly exposed: `yes/no`
- Профіль: `TODO` (назва JSON-профілю, якщо застосовно)

## 2. Purpose

### Business purpose

`TODO`

### Lifecycle

- Created when: `TODO`
- Updated when: `TODO`
- Deactivated/archived when: `TODO`
- Deleted when: `TODO`
- Immutable fields: `TODO`

## 3. SQLite schema

> SQL описує структуру SQLite. Включи фінальну схему, обмеження та коментарі для неочевидних рішень.

```sql
-- TODO: CREATE TABLE ...
-- TODO: primary key
-- TODO: columns, types, NULL/NOT NULL, defaults
-- TODO: foreign keys and ON DELETE behavior
-- TODO: CHECK constraints
```

### Column semantics

| Column | Required | SQLite type | Default | Meaning | Mutable | Sensitive |
|---|---:|---|---|---|---:|---:|
| `TODO` | yes/no | `TODO` | `TODO` | `TODO` | yes/no | yes/no |

## 4. Indexes and uniqueness

```sql
-- TODO: CREATE UNIQUE INDEX ...
-- TODO: CREATE INDEX ...
```

| Index | Columns/expression | Unique | Partial condition | Reason |
|---|---:|---|---|---|
| `TODO` | `TODO` | yes/no | `TODO` | `TODO` |

## 5. Relationships

| Related entity | Relation | Required | FK owner | Delete behavior | Additional rules |
|---|---:|---|---|---|---|
| `TODO` | one-to-one / one-to-many / many-to-many | yes/no | `TODO` | restrict/cascade/set-null | `TODO` |

## 6. Value Objects

| Value Object | Backing fields | Invariants | Normalization | SQL representation | API representation |
|---|---|---|---|---|---|
| `TODO` | `TODO` | `TODO` | `TODO` | scalar/owned/complex | string/object |

## 7. Domain rules and invariants

### Valid states

- `TODO`

### Invalid states

- `TODO`

### Create rules

- `TODO`

### Update rules

- `TODO`

### Delete/archive rules

- `TODO`

### Cross-entity rules

- `TODO`

## 8. Validation

| Field/rule | Validation | Layer | Error code/message |
|---|---|---|---|
| `TODO` | `TODO` | Domain/Application/DB | `TODO` |

## 9. Application contract

### Commands

| Command | Input | Preconditions | Result | Errors |
|---|---|---|---|---|
| `TODO` | `TODO` | `TODO` | `TODO` | `TODO` |

### Queries

| Query | Input | Result/read model | Filtering/sorting/paging |
|---|---|---|---|
| `TODO` | `TODO` | `TODO` | `TODO` |

### Ports

```text
TODO: List repository, external service or other ports required by the use cases.
```

## 10. API contract

| Method | Route | Authorization | Request | Response | Errors |
|---|---|---|---|---|---|
| `TODO` | `TODO` | `TODO` | `TODO` | `TODO` | `TODO` |

## 11. 1C mapping (COM→SQLite)

```text
TODO: Як поля 1C мапляться на колонки SQLite.
TODO: Як обробляються Ref (GUID), Enum (string), tabular sections.
```

### Mapping decisions

- COM value mapper strategy: `TODO`
- Ref handling: `TODO` (GUID string)
- Tabular sections: `TODO` (окремі таблиці vs JSON)
- Incremental update field: `TODO` (по якому полю відстежуємо зміни)

## 12. Migration and existing data

- Existing rows affected: `yes/no`
- Backfill required: `yes/no`
- Data-loss risk: `TODO`

## 13. Tests and acceptance criteria

### Domain tests

- `TODO`

### Application tests

- `TODO`

### Infrastructure tests

- `TODO`

### Acceptance criteria

- [ ] TC1: COM reader коректно мапить значення.
- [ ] TC2: Значення зберігаються в SQLite згідно зі схемою.
- [ ] TC3: Інкрементальне оновлення працює по вказаному полю.
- [ ] TC4: Тести проходять (`dotnet test`).

## 14. Implementation output

After this template is approved, the implementation should produce:

- Domain entity/aggregate and Value Objects;
- Application commands, queries, DTOs, validators and ports;
- SQLite schema and repositories;
- COM reader/mapper;
- CLI/API integration;
- unit and integration tests;
- related task files and documentation updates.

## 15. Open decisions

- `TODO`