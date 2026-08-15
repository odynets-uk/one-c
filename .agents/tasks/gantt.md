# Gantt-діаграма залежностей

```mermaid
gantt
    title OneC Sync Project
    dateFormat  YYYY-MM-DD
    axisFormat  %d %b

    section Фаза 0
    Документація           :task-000, 2026-08-08, 1d

    section Фаза 1
    COM-з'єднання          :task-001, after task-000, 3d

    section Фаза 2
    Парсер XSD             :task-002, after task-001, 3d

    section Фаза 3
    Профілі (абстракція)   :task-003, after task-002, 2d

    section Фаза 4
    Динамічний читач       :task-004, after task-003, 4d

    section Фаза 4b
    Витяг змінених (changed_since) :task-004b, after task-004a, 2d

    section Фаза 5
    SQLite + Синхронізатор :task-005, after task-004b, 5d

    section Фаза 6
    API                    :task-006, after task-005, 3d
```

## Таблиця залежностей

| Задача | Залежить від | Критичний шлях |
|---|---|---|
| `task-000-docs` | — | ✅ |
| `task-001-com-connection` | `task-000-docs` | ✅ |
| `task-002-xsd-parser` | `task-001-com-connection` | ✅ |
| `task-003-profiles` | `task-002-xsd-parser` | ✅ |
| `task-004-catalog-reader` | `task-003-profiles` | ✅ |
| `task-004a-performance` | `task-004-catalog-reader` | ✅ |
| `task-004b-changed-since` | `task-004a-performance` | ✅ |
| `task-005-sync-sqlite` | `task-004b-changed-since` | ✅ |
| `task-006-api` | `task-005-sync-sqlite` | ✅ |

**Критичний шлях**: 0 → 1 → 2 → 3 → 4 → 4a → 4b → 5 → 6

## Правила оновлення

- Після зміни задач — оновити цей файл та `index.md`
- Після завершення задачі — оновити статус у `index.md` та Action log у відповідному task-файлі