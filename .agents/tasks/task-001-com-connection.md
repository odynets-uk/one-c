# Задача: COM-з'єднання з 1С

| | |
|---|---|
| **ID** | `task-001-com-connection` |
| **Статус** | `done` |
| **Залежності** | `task-000-docs` |
| **Оцінка** | 3d |
| **Пріоритет** | high |

## Мета

Реалізувати надійне COM-з'єднання з базою 1С через `comcntr.dll` (x32) та команду `test-connection`, яка підключається і виводить версії платформи та конфігурації.

## Обсяг

- [x] Додати Serilog (Console + File) в `OneC.Infrastructure`
- [x] `OneC.Infrastructure/Com/ComConnector.cs` — обгортка над `V83.COMConnector` (dynamic, IDispatch, Marshal.ReleaseComObject)
- [x] `OneC.Infrastructure/Com/ComSession.cs` — життєвий цикл: Connect/Dispose, обробка HRESULT, логування
- [x] `OneC.Infrastructure/Security/ConnectionStringProtector.cs` — AES шифрування/дешифрування пароля (ключ у коді)
- [x] `OneC.Cli` команда `test-connection` — підключення, вивід версій
- [x] Налаштування x86 (Prefer32Bit) для `OneC.Cli.csproj`
- [x] `OneC.Cli/appsettings.json` — секція `Com` (ConnectionString, Encryption)

## Критерії готовності

- [x] `dotnet run --project OneC.Cli -- test-connection` успішно підключається до бази Kplus
- [x] Виводить версію платформи (`СистемнаяИнформация.ВерсияПриложения`) та конфігурації (`Метаданные.Версия`)
- [x] При відсутності з'єднання — логується зрозуміла помилка (Serilog)
- [x] Пароль у `appsettings.local.json` зашифрований (AES)
- [x] Тести для `ConnectionStringProtector` (unit) проходять
- [x] Коміт створено з multiline повідомленням

## Технічні рішення / Контекст

- ProgID: `V83.COMConnector`, CLSID: `{181E893D-73A4-4722-B61D-D604B3D67D47}`
- Бібліотека вже зареєстрована (користувач контролює сам)
- Рядок з'єднання: `File="K:\Portables\1cv8\1c-com-module\1CConector\KplusBase";Usr="Менеджер2";Pwd="4715";`
- Пароль шифрується AES з фіксованим ключем у коді (не enterprise рівень)
- CLI має бути x86 (Prefer32Bit) через 32-бітну comcntr.dll
- При відсутності з'єднання — логувати помилку, не падати з незрозумілим exception

## Action log

| Дата | Дія | Результат |
|---|---|---|
| 2026-08-08 | Задача створена | backlog |
| 2026-08-08 | Реалізовано COM-з'єднання, test-connection працює (SELECT 1) | done |
