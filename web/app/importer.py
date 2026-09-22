"""Загрузка отчётов в базу.

Отчёты считает движок на C#:
    dotnet run --project src/RepoHealth.Cli -- export data/facts data/reports.jsonl

Здесь мы только раскладываем готовое по таблице.

Запуск:
    python -m app.importer ../backend/data/reports.jsonl
"""

import json
import sys

from . import db


def load(path: str) -> int:
    db.prepare()

    loaded = 0
    broken = 0

    with open(path, encoding="utf-8") as source:
        for number, line in enumerate(source, start=1):
            line = line.strip()
            if not line:
                continue

            try:
                db.save(json.loads(line))
                loaded += 1
            except (json.JSONDecodeError, KeyError) as error:
                broken += 1
                print(f"  строка {number} пропущена: {error}")

    print(f"загружено отчётов: {loaded}")
    if broken:
        print(f"пропущено строк: {broken}")

    return loaded


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit("Укажите файл: python -m app.importer <reports.jsonl>")

    load(sys.argv[1])
