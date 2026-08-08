# Задача: Абстракція профілів вибірки

| | |
|---|---|
| **ID** | `task-003-profiles` |
| **Статус** | `done` |
| **Залежності** | `task-002-xsd-parser` |
| **Оцінка** | 2d |
| **Пріоритет** | normal |

## Мета

Створити абстрактний рівень профілів вибірки (моделі + loader), без конкретних профілів. Конкретні профілі (`catalog`, `products`, `expenses`) — окремий таск після опису користувачем.

## Обсяг

- [x] `OneC.Domain/Profiles/` — абстрактні моделі: `ExtractionProfile`, `OutputSettings`, `Filters`, `IncludeFields`
- [x] `OneC.Infrastructure/Profiles/ProfileLoader.cs` — завантаження/валідація JSON-профілів
- [x] `OneC.Cli` команда `list-profiles` — список доступних профілів (порожній на цьому етапі)

## Критерії готовності

- [x] Моделі профілів відповідають структурі з прикладу `products.json`
- [x] `ProfileLoader` валідує JSON і кидає зрозумілі помилки
- [x] Тести валідації (unit) проходять
- [x] Коміт створено з multiline повідомленням

## Технічні рішення / Контекст

- Приклад структури профілю (products):
  ```json
  {
    "output": { "path": "...", "pretty": true },
    "filters": { "prices": {...}, "stock": {...}, "items": {...} },
    "include_fields": { "item": [...], "price": [...], "stock": [...] },
    "skip_items_without_prices": false,
    "skip_items_without_stock": false
  }
  ```
- Конкретні профілі не розробляються — лише абстракція

## Action log

| Дата | Дія | Результат |
|---|---|---|
| 2026-08-08 | Задача створена | backlog |
| 2026-08-08 | Створено абстрактні моделі профілів та ProfileLoader | done |
