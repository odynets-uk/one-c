# Задача: Парсер XSD-схеми

| | |
|---|---|
| **ID** | `task-002-xsd-parser` |
| **Статус** | `done` |
| **Залежності** | `task-001-com-connection` |
| **Оцінка** | 3d |
| **Пріоритет** | high |

## Мета

Реалізувати потоковий парсер `data-enterprise.xsd` (~1.9 МБ) для отримання структури метаданих 1С: довідники, переліки, табличні частини.

## Обсяг

- [x] `OneC.Domain/Metadata/` — моделі: `CatalogDefinition`, `FieldDefinition`, `EnumDefinition`, `TabularSectionDefinition`, `FieldType`
- [x] `OneC.Infrastructure/Metadata/XsdMetadataParser.cs` — потоковий парсинг (XmlReader)
- [x] `OneC.Infrastructure/Metadata/MetadataCache.cs` — кешування результату (JSON-файл)
- [x] `OneC.Cli` команда `list-catalogs` — список довідників з XSD
- [x] `OneC.Cli` команда `list-enums` — переліки та їх значення

## Критерії готовності

- [ ] `list-catalogs` виводить список довідників (назва, поля, типи)
- [ ] `list-enums` виводить переліки та їх значення
- [ ] Парсер обробляє `CatalogObject.*`, `CatalogRef.*`, `EnumRef.*`, `CatalogTabularSectionRow.*`
- [ ] Тести парсера (unit, на зразку XSD) проходять
- [ ] Коміт створено з multiline повідомленням

## Технічні рішення / Контекст

- XSD: `data-enterprise.xsd` в корені проекту (в .gitignore)
- Зразок: `CatalogObject.Номенклатура.xsd` — типова структура
- Парсинг потоковий (XmlReader), не завантажувати весь файл у пам'ять
- Кешування результату в JSON для швидкого повторного запуску

## Action log

| Дата | Дія | Результат |
|---|---|---|
| 2026-08-08 | Задача створена | backlog |
| 2026-08-08 | Реалізовано XSD-парсер, list-catalogs (156), list-enums (298) | done |
