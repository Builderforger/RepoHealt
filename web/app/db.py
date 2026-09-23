"""Хранилище отчётов.

Прототип работает на SQLite: файл рядом с приложением, никакой установки.
Схема написана так, чтобы переехать на PostgreSQL без изменений запросов.
"""

import json
import os
import sqlite3
from contextlib import contextmanager
from pathlib import Path

# Путь считаем от самого приложения, а не от текущей папки:
# иначе база «теряется» при запуске из другого места.
DEFAULT_DB = Path(__file__).resolve().parent.parent / "repohealth.db"

DB_PATH = os.environ.get("REPOHEALTH_DB", str(DEFAULT_DB))

SCHEMA = """
CREATE TABLE IF NOT EXISTS reports (
    repo          TEXT PRIMARY KEY,
    url           TEXT NOT NULL,
    score         REAL,            -- NULL означает «оценить не удалось»
    language      TEXT,
    likes         REAL,
    last_activity TEXT,
    scored        INTEGER NOT NULL DEFAULT 0,  -- сколько категорий посчитано
    methodology   TEXT,
    analyzed_at   TEXT,
    report        TEXT NOT NULL    -- отчёт целиком, как его отдал расчёт
);

CREATE INDEX IF NOT EXISTS reports_score    ON reports(score DESC);
CREATE INDEX IF NOT EXISTS reports_likes    ON reports(likes DESC);
CREATE INDEX IF NOT EXISTS reports_language ON reports(language);

-- Личные разборы лежат отдельно от публичного рейтинга.
-- Так устроено намеренно: пользователь видит их своим токеном,
-- и среди них бывают закрытые репозитории, которым в публичной
-- витрине не место.
CREATE TABLE IF NOT EXISTS personal_reports (
    user_id     TEXT NOT NULL,
    repo        TEXT NOT NULL,
    score       REAL,
    analyzed_at TEXT,
    report      TEXT NOT NULL,
    PRIMARY KEY (user_id, repo)
);
"""


@contextmanager
def connect():
    connection = sqlite3.connect(DB_PATH)
    connection.row_factory = sqlite3.Row
    try:
        yield connection
        connection.commit()
    finally:
        connection.close()


def prepare():
    """Создаёт таблицы, если их ещё нет."""
    with connect() as connection:
        connection.executescript(SCHEMA)


def save(report: dict):
    """Кладёт отчёт в базу, заменяя прежний для этого проекта."""
    scored = sum(
        1 for category in report.get("categories", [])
        if category.get("status") == "Scored"
    )

    with connect() as connection:
        connection.execute(
            """
            INSERT INTO reports
                (repo, url, score, language, likes, last_activity,
                 scored, methodology, analyzed_at, report)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(repo) DO UPDATE SET
                url = excluded.url,
                score = excluded.score,
                language = excluded.language,
                likes = excluded.likes,
                last_activity = excluded.last_activity,
                scored = excluded.scored,
                methodology = excluded.methodology,
                analyzed_at = excluded.analyzed_at,
                report = excluded.report
            """,
            (
                report.get("repo", ""),
                report.get("url", ""),
                report.get("score"),
                report.get("language"),
                report.get("likes"),
                report.get("last_activity"),
                scored,
                report.get("methodology_version"),
                report.get("analyzed_at"),
                json.dumps(report, ensure_ascii=False),
            ),
        )


# Разрешённые способы сортировки.
# Список закрытый: подставлять в запрос то, что пришло из адресной
# строки, нельзя — это дыра в безопасности.
SORTS = {
    "score": "score DESC NULLS LAST, repo",
    "likes": "likes DESC, repo",
    "activity": "last_activity DESC, repo",
}


def rating(language: str | None = None, sort: str = "score",
           page: int = 1, per_page: int = 50):
    """Страница рейтинга: строки и общее число проектов."""
    order = SORTS.get(sort, SORTS["score"])

    where, params = "", []
    if language:
        where = "WHERE language = ?"
        params.append(language)

    offset = max(page - 1, 0) * per_page

    with connect() as connection:
        total = connection.execute(
            f"SELECT COUNT(*) FROM reports {where}", params
        ).fetchone()[0]

        rows = connection.execute(
            f"""
            SELECT repo, url, score, language, likes, last_activity, scored
            FROM reports {where}
            ORDER BY {order}
            LIMIT ? OFFSET ?
            """,
            (*params, per_page, offset),
        ).fetchall()

    return [dict(row) for row in rows], total


def languages():
    """Языки для фильтра — только те, что реально встречаются."""
    with connect() as connection:
        rows = connection.execute(
            """
            SELECT language, COUNT(*) AS amount
            FROM reports
            WHERE language IS NOT NULL AND language <> ''
            GROUP BY language
            ORDER BY amount DESC
            """
        ).fetchall()

    return [dict(row) for row in rows]


def report(repo: str) -> dict | None:
    """Отчёт по одному проекту."""
    with connect() as connection:
        row = connection.execute(
            "SELECT report FROM reports WHERE repo = ?", (repo,)
        ).fetchone()

    return json.loads(row["report"]) if row else None


# ---------- личные разборы ----------

def save_personal(user_id: str, report: dict):
    """Сохраняет разбор в личный кабинет пользователя.
    В публичный рейтинг эти данные не попадают."""
    with connect() as connection:
        connection.execute(
            """
            INSERT INTO personal_reports (user_id, repo, score, analyzed_at, report)
            VALUES (?, ?, ?, ?, ?)
            ON CONFLICT(user_id, repo) DO UPDATE SET
                score = excluded.score,
                analyzed_at = excluded.analyzed_at,
                report = excluded.report
            """,
            (
                user_id,
                report.get("repo", ""),
                report.get("score"),
                report.get("analyzed_at"),
                json.dumps(report, ensure_ascii=False),
            ),
        )


def personal_reports(user_id: str) -> list[dict]:
    with connect() as connection:
        rows = connection.execute(
            """
            SELECT repo, score, analyzed_at
            FROM personal_reports
            WHERE user_id = ?
            ORDER BY analyzed_at DESC
            """,
            (user_id,),
        ).fetchall()

    return [dict(row) for row in rows]


def personal_report(user_id: str, repo: str) -> dict | None:
    """Отчёт отдаётся только тому, кто его заказывал."""
    with connect() as connection:
        row = connection.execute(
            "SELECT report FROM personal_reports WHERE user_id = ? AND repo = ?",
            (user_id, repo),
        ).fetchone()

    return json.loads(row["report"]) if row else None


def summary() -> dict:
    """Короткая сводка для шапки рейтинга."""
    with connect() as connection:
        row = connection.execute(
            """
            SELECT COUNT(*) AS total,
                   COUNT(score) AS with_score,
                   AVG(score) AS average,
                   MAX(analyzed_at) AS analyzed_at
            FROM reports
            """
        ).fetchone()

    return dict(row)
