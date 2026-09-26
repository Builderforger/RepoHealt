"""Подхват свежих отчётов и статус пересчёта.

Пересчётом занимается движок: он обходит каталог по расписанию
и кладёт рядом два файла — отчёты и статус прохода. Сайту остаётся
заметить, что отчёты обновились, и перелить их в свою базу.
"""

import asyncio
import json
import os
from pathlib import Path

from . import db, importer

DATA = Path(os.environ.get("REPOHEALTH_DATA", "/data"))
REPORTS = DATA / "reports.jsonl"
STATUS = DATA / "last-run.json"

# Как часто заглядывать, не обновились ли отчёты.
CHECK_EVERY = int(os.environ.get("REPOHEALTH_CHECK_SECONDS", "60"))

_last_seen: float | None = None


def status() -> dict | None:
    """Что сейчас с пересчётом: когда шёл, сколько собрал, чем кончился."""
    try:
        with open(STATUS, encoding="utf-8") as file:
            return json.load(file)
    except (OSError, json.JSONDecodeError):
        return None


def load_if_new() -> int:
    """Заливает отчёты в базу, если файл изменился с прошлого раза."""
    global _last_seen

    try:
        changed_at = REPORTS.stat().st_mtime
    except OSError:
        return 0

    if _last_seen is not None and changed_at <= _last_seen:
        return 0

    loaded = importer.load(str(REPORTS))
    _last_seen = changed_at

    return loaded


async def watch():
    """Фоновая проверка: не появились ли свежие отчёты."""
    while True:
        try:
            loaded = await asyncio.to_thread(load_if_new)
            if loaded:
                print(f"подхвачено свежих отчётов: {loaded}", flush=True)
        except Exception as error:
            print(f"не удалось обновить отчёты: {error}", flush=True)

        await asyncio.sleep(CHECK_EVERY)
