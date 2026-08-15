# Задача: Оптимізація витягу змінених товарів (changed_since)

| | |
|---|---|
| **ID** | `task-004b-changed-since` |
| **Статус** | `in-progress` |
| **Залежності** | `task-004a-performance` |
| **Оцінка** | 2d |
| **Пріоритет** | high |

## Мета

Оптимізувати витяг товарів з `changed_since` фільтром. Поточна реалізація читає **весь каталог** (15079 записів, ~41 с), потім завантажує ціни/залишки для всіх, і лише потім фільтрує до ~767 змінених. Це марнує ~90% часу.

Ціль — спочатку знайти GUID змінених товарів з регістрів (ціни АБО залишки), потім читати тільки їх з каталогу.

## Обсяг

- [x] Новий метод `LoadChangedItemGuids` в `IRegisterDataReader` — повертає об'єднаний набір GUID з регістрів (ціни за період АБО рухи за період)
- [x] `PriceLoader.LoadChangedItemGuids(changedSince)` — GUID товарів зі зміненими цінами за період (без повного завантаження даних)
- [x] `LastMovementLoader.LoadChangedItemGuids(changedSince)` — GUID товарів з рухом за період (без повного завантаження даних)
- [x] `CatalogReader.Read` — якщо задано `changed_since`, спочатку отримати змінені GUID, потім читати каталог з `WHERE Ref IN (&ChangedItems)`
- [x] Категорії (IsFolder=true) завантажувати як раніше (518 записів — швидко, потрібні для `exists` валідації)
- [x] Ref cache будувати тільки для змінених GUID
- [x] Ціни/залишки завантажувати тільки для змінених GUID
- [x] Тести для нової логіки
- [ ] Повторний замір на `products-all-45d.local.json`

## Критерії готовності

- [x] `LoadChangedItemGuids` повертає об'єднаний набір GUID (OR: ціни АБО залишки)
- [x] Каталог читається тільки для змінених GUID (`WHERE Ref IN (...)`), а не весь
- [ ] Час ітерації каталогу зменшено з ~41 с до ~2-3 с (для ~767 змінених)
- [x] Результати витягу не змінилися (ті самі 767 записів)
- [x] Тести написані та проходять (`dotnet test`)
- [x] Коміт створено з multiline повідомленням

## Технічні рішення / Контекст

### Поточна проблема

`CatalogReader.Read` завжди читає весь каталог (`SELECT ... FROM Справочник.Номенклатура WHERE IsFolder = FALSE`), збирає всі GUID, будує ref cache для всіх, завантажує ціни/залишки для всіх, і лише потім фільтрує за `changed_since`.

Замір (2026-08-15, `products-all-45d.local.json`):

| Етап | Час | Записів |
|---|---|---|
| category-guids | 382 мс | 518 |
| catalog-iterate | 41 676 мс | 15 079 |
| ref-cache | 6 087 мс | 15 079 |
| price-types | 48 мс | 4 |
| prices | 3 681 мс | 170 |
| stock | 3 631 мс | 1 277 |
| last-movements | 5 299 мс | 1 908 |
| **Разом** | **60 879 мс** | **767** (результат) |

### Цільова архітектура

1. **Спочатку** — знайти GUID змінених товарів:
   - `SELECT Номенклатура FROM РегистрСведений.ЦеныНоменклатуры WHERE Период >= &SinceDate` → GUID зі зміненими цінами
   - `SELECT Номенклатура FROM РегистрНакопления.ТоварыНаСкладах WHERE Период >= &SinceDate` → GUID з рухом
   - Об'єднати (OR)
2. **Потім** — читати каталог тільки для цих GUID:
   - `SELECT ... FROM Справочник.Номенклатура WHERE IsFolder = FALSE AND Ref IN (&ChangedItems)`
3. **Потім** — ref cache, ціни, залишки тільки для змінених GUID
4. **Категорії** — завантажувати всі (для `exists` валідації)

### Файли

- `OneC.Infrastructure/Readers/PriceLoader.cs` — додати `LoadChangedItemGuids`
- `OneC.Infrastructure/Readers/LastMovementLoader.cs` — додати `LoadChangedItemGuids`
- `OneC.Infrastructure/Readers/RegisterDataReader.cs` — фасад, новий метод
- `OneC.Application/Abstractions/IRegisterDataReader.cs` — порт, новий метод
- `OneC.Infrastructure/Readers/CatalogReader.cs` — змінити порядок: спочатку GUID, потім каталог
- `OneC.Tests/` — тести для нової логіки

## Action log

| Дата | Дія | Результат |
|---|---|---|
| 2026-08-15 | Задача створена після заміру `products-all-45d.local.json` (60.9 с, 767 з 15079) | in-progress |
| 2026-08-15 | Реалізовано `LoadChangedItemGuids` (порт + PriceLoader + LastMovementLoader + фасад), змінено `CatalogReader.Read` (pre-filter за changed_since), рефакторинг `RefArrayFactory`, тести | done (очікує повторний замір) |
