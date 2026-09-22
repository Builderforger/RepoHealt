# Веб-часть

Сайт показывает рейтинг здоровья и страницу разбора по каждому проекту.
Сам расчёт живёт в `backend` — сюда приезжают уже готовые отчёты.

## Как данные попадают на сайт

```
C# collect  →  факты (JSON, один файл на проект)
C# export   →  отчёты (JSONL, по отчёту на строку)
importer    →  SQLite
FastAPI     →  страницы
```

## Запуск

Подготовка окружения — один раз:

```bash
python -m venv .venv
.venv/Scripts/python.exe -m pip install -r requirements.txt
```

Собрать данные и посчитать отчёты (из папки `backend`, нужен `SC_TOKEN`):

```bash
dotnet run --project src/RepoHealth.Cli -- collect data/facts --limit 200
dotnet run --project src/RepoHealth.Cli -- export data/facts data/reports.jsonl
```

Загрузить отчёты в базу:

```bash
.venv/Scripts/python.exe -m app.importer ../backend/data/reports.jsonl
```

Поднять сайт:

```bash
.venv/Scripts/python.exe -m uvicorn app.main:app --port 8010
```

Открыть `http://localhost:8010`.

## Адреса

| Адрес | Что отдаёт |
| --- | --- |
| `/` | рейтинг: фильтр по языку, сортировка по оценке, лайкам и активности |
| `/repo/{org}/{name}` | разбор проекта: категории, части, «нет данных» с причинами |
| `/repo/{org}/{name}/report.md` | отчёт файлом в Markdown |
| `/api/repos` | рейтинг в JSON |
| `/api/repos/{org}/{name}` | отчёт в JSON |

## Хранилище

SQLite, файл `repohealth.db` рядом с приложением. Путь можно задать
переменной окружения `REPOHEALTH_DB`.

Отчёт хранится целиком в колонке `report`, а поля для рейтинга
(оценка, язык, лайки, дата) продублированы отдельными колонками —
по ним построены индексы для сортировки и фильтра.

Схема написана обычным SQL без особенностей SQLite: переезд
на PostgreSQL потребует только смены драйвера.
