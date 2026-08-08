# Задача: Синхронізатор та SQLite

| | |
|---|---|
| **ID** | `task-005-sync-sqlite` |
| **Статус** | `backlog` |
| **Залежності** | `task-004-catalog-reader` |
| **Оцінка** | 5d |
| **Пріоритет** | high |

## Мета

Реалізувати збереження даних у SQLite та оркестрацію синхронізації (повний дамп / інкрементальний).

## Обсяг

- [ ] `OneC.Infrastructure/Persistence/SqliteConnectionFactory.cs` — підключення до SQLite (Microsoft.Data.Sqlite)
- [ ] `OneC.Infrastructure/Persistence/SchemaInitializer.cs` — створення таблиць (items, prices, stock, sync_log)
- [ ] `OneC.Infrastructure/Repositories/ItemRepository.cs`, `PriceRepository.cs`, `StockRepository.cs`
- [ ] `OneC.Application/Services/SyncService.cs` — оркестрація: повний дамп / інкрементальний (по `changed_since`)
- [ ] `OneC.Cli` команда `sync --profile <profile> [--mode full|incremental] [--since 1d|24h|1w]`
- [ ] `SingleInstance` — захист від паралельних запусків (з конфіга)

## Критерії готовності

- [ ] `sync` зберігає дані в SQLite
- [ ] Повний дамп та інкрементальний режим працюють
- [ ] `SingleInstance` блокує паралельні запуски
- [ ] Тести репозиторіїв (integration з SQLite) проходять
- [ ] Коміт створено з multiline повідомленням

## Технічні рішення / Контекст

- SQLite через Microsoft.Data.Sqlite
- Таблиці: items, prices, stock, sync_log
- Інкрементальний режим — по `changed_since` (1d, 24h, 1w)
- `SingleInstance` — з конфіга `SingleInstance:Enabled`

## Action log

| Дата | Дія | Результат |
|---|---|---|
| 2026-08-08 | Задача створена | backlog |