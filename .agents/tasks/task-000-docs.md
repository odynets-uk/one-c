# Задача: Створення документації проекту

| | |
|---|---|
| **ID** | `task-000-docs` |
| **Статус** | `done` |
| **Залежності** | `none` |
| **Оцінка** | 1d |
| **Пріоритет** | high |

## Мета

Створити базову документацію проекту: `AGENTS.md`, `README.md`, структуру `.agents/tasks/` та шаблони, щоб подальша робота (включно з новими сесіями) була зрозумілою.

## Обсяг

- [x] Створити `.agents/templates/task-template.md` — шаблон задачі
- [x] Створити `.agents/templates/entity-design-template.md` — шаблон опису сутності/профілю
- [x] Створити `.agents/tasks/index.md` — індекс задач
- [x] Створити `.agents/tasks/gantt.md` — Gantt-діаграма залежностей
- [x] Створити `AGENTS.md` — правила розробки, архітектура, git-workflow
- [x] Створити `README.md` — огляд, швидкий старт, конфігурація

## Критерії готовності

- [x] Всі файли створені
- [x] `AGENTS.md` містить: архітектуру, DI, тестування, git-workflow (multiline commits), change checklist
- [x] `README.md` містить: стек, структуру, конфігурацію, запуск
- [x] Коміт створено з multiline повідомленням

## Технічні рішення / Контекст

- Проект: OneC — CLI-синхронізатор даних з 1С (COM) у SQLite + API для читання
- Solo-розробка, без CI
- Стек: .NET 10, C#, COM (comcntr.dll x32), SQLite, Serilog
- Документація адаптована з `docs-examples/` (AGENTS.md, README.md, ENTITY-DESIGN-TEMPLATE.md)

## Action log

| Дата | Дія | Результат |
|---|---|---|
| 2026-08-08 | Створено шаблони task та entity-design | done |
| 2026-08-08 | Створено index.md, gantt.md, AGENTS.md, README.md | done |
