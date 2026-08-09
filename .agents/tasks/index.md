# Індекс задач

| ID | Назва | Статус | Залежності | Оцінка |
|---|---|---|---|---|
| `task-000-docs` | Створення документації проекту | `done` | `none` | 1d |
| `task-001-com-connection` | COM-з'єднання з 1С | `done` | `task-000-docs` | 3d |
| `task-002-xsd-parser` | Парсер XSD-схеми | `done` | `task-001-com-connection` | 3d |
| `task-003-profiles` | Абстракція профілів вибірки | `done` | `task-002-xsd-parser` | 2d |
| `task-004-catalog-reader` | Динамічний читач даних | `done` | `task-003-profiles` | 4d |
| `task-005-sync-sqlite` | Синхронізатор та SQLite | `backlog` | `task-004-catalog-reader` | 5d |
| `task-006-api` | API для читання даних | `backlog` | `task-005-sync-sqlite` | 3d |

## Статуси

- `backlog` — задача запланована, не розпочата
- `in-progress` — задача в роботі
- `done` — задача завершена

## Правила оновлення

- Після завершення задачі — оновити статус на `done` та додати запис в Action log
- Після зміни задач — оновити цей файл та `gantt.md`