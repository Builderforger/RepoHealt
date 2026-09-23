"""Обращения к SourceCraft от имени пользователя и запуск разбора.

Список репозиториев берём прямо здесь: это один запрос.
А сбор фактов и расчёт оценки делает движок на C# —
чтобы оценка считалась одним и тем же кодом и для публичного
рейтинга, и для личного кабинета.
"""

import json
import os
import subprocess
import urllib.parse
import urllib.request
from pathlib import Path

API = "https://api.sourcecraft.tech"

BACKEND = Path(__file__).resolve().parent.parent.parent / "backend"
CLI_PROJECT = BACKEND / "src" / "RepoHealth.Cli"

ANALYSIS_TIMEOUT = 300


def my_repositories(token: str) -> list[dict] | None:
    """Репозитории, доступные пользователю по его токену.
    Сюда попадают и закрытые — показывать их можно только ему."""
    repositories = []
    page_token = ""

    for _ in range(20):
        url = f"{API}/me/repos?page_size=100&page_token={urllib.parse.quote(page_token)}"
        request = urllib.request.Request(url, headers={"Authorization": f"Bearer {token}"})

        try:
            with urllib.request.urlopen(request, timeout=30) as answer:
                page = json.load(answer)
        except Exception:
            return None

        for repo in page.get("repositories", []):
            organization = (repo.get("organization") or {}).get("slug", "")

            repositories.append({
                "name": f"{organization}/{repo.get('slug', '')}",
                "url": repo.get("web_url", ""),
                "language": (repo.get("language") or {}).get("name"),
                "private": repo.get("visibility") != "public",
                "empty": bool(repo.get("is_empty")),
                "updated": repo.get("last_updated"),
            })

        page_token = page.get("next_page_token") or ""
        if not page_token:
            break

    return repositories


ENGINE = os.environ.get("REPOHEALTH_ENGINE", "")


def analyse(repo_name: str, token: str) -> dict | None:
    """Разбирает один репозиторий движком на C#.

    В собранном виде движок работает отдельной службой, и мы зовём
    его по сети. Без неё (например, при разработке на своей машине)
    запускаем ту же логику командой.
    """
    if ENGINE:
        return _analyse_over_network(repo_name, token)

    return _analyse_by_command(repo_name, token)


def _analyse_over_network(repo_name: str, token: str) -> dict | None:
    body = json.dumps({"repo": repo_name}).encode()

    request = urllib.request.Request(
        f"{ENGINE.rstrip('/')}/analyze",
        data=body,
        headers={
            "Content-Type": "application/json",
            # Токен пользователя идёт заголовком и живёт только
            # на время запроса: движок его не сохраняет.
            "X-SourceCraft-Token": token,
        },
    )

    try:
        with urllib.request.urlopen(request, timeout=ANALYSIS_TIMEOUT) as answer:
            return json.load(answer)
    except Exception:
        return None


def _analyse_by_command(repo_name: str, token: str) -> dict | None:
    """Запасной путь для запуска без контейнеров.

    Токен передаётся переменной окружения, а не аргументом:
    аргументы процесса видны в системе, переменные — нет.
    """
    environment = dict(os.environ, SC_TOKEN=token)

    try:
        finished = subprocess.run(
            ["dotnet", "run", "--project", str(CLI_PROJECT), "--",
             "analyze", repo_name],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            env=environment,
            cwd=str(BACKEND),
            timeout=ANALYSIS_TIMEOUT,
        )
    except subprocess.TimeoutExpired:
        return None

    if finished.returncode != 0:
        return None

    # dotnet run может печатать свои сообщения перед отчётом,
    # поэтому берём текст начиная с первой фигурной скобки.
    output = finished.stdout
    start = output.find("{")

    if start < 0:
        return None

    try:
        return json.loads(output[start:])
    except json.JSONDecodeError:
        return None
