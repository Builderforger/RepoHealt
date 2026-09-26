"""Вход через Яндекс ID.

Яндекс ID отвечает только на вопрос «кто это». Доступ к данным
репозиториев он не даёт — для этого пользователь отдельно указывает
свой токен SourceCraft. Так и задумано организаторами.

Приложение регистрируется на https://oauth.yandex.ru/client/new,
оттуда берутся идентификатор и пароль приложения.
"""

import os
import json
import secrets
import time
import urllib.parse
import urllib.request

CLIENT_ID = os.environ.get("YANDEX_CLIENT_ID", "")
CLIENT_SECRET = os.environ.get("YANDEX_CLIENT_SECRET", "")
REDIRECT_URI = os.environ.get("YANDEX_REDIRECT_URI", "http://localhost:8010/auth/callback")

AUTHORIZE_URL = "https://oauth.yandex.ru/authorize"
TOKEN_URL = "https://oauth.yandex.ru/token"
USER_INFO_URL = "https://login.yandex.ru/info?format=json"

# Сколько живёт сессия без обращений.
SESSION_LIFETIME = 8 * 60 * 60


def configured() -> bool:
    """Настроен ли вход. Без ключей приложение работает,
    но кабинет недоступен — и об этом честно пишется на странице."""
    return bool(CLIENT_ID and CLIENT_SECRET)


def secure_cookies() -> bool:
    """На стенде с сертификатом печенье сеанса не должно
    уходить по открытому http. Локально адрес возврата
    начинается с http, и тогда запрет не нужен."""
    return REDIRECT_URI.startswith("https://")


def login_url(state: str) -> str:
    query = urllib.parse.urlencode({
        "response_type": "code",
        "client_id": CLIENT_ID,
        "redirect_uri": REDIRECT_URI,
        "state": state,
    })
    return f"{AUTHORIZE_URL}?{query}"


def exchange_code(code: str) -> str | None:
    """Меняет одноразовый код на токен Яндекса."""
    body = urllib.parse.urlencode({
        "grant_type": "authorization_code",
        "code": code,
        "client_id": CLIENT_ID,
        "client_secret": CLIENT_SECRET,
    }).encode()

    request = urllib.request.Request(TOKEN_URL, data=body)

    try:
        with urllib.request.urlopen(request, timeout=20) as answer:
            return json.load(answer).get("access_token")
    except Exception:
        return None


def who_is_it(yandex_token: str) -> dict | None:
    """Узнаёт, кто вошёл. Храним только идентификатор и отображаемое имя."""
    request = urllib.request.Request(
        USER_INFO_URL, headers={"Authorization": f"OAuth {yandex_token}"})

    try:
        with urllib.request.urlopen(request, timeout=20) as answer:
            info = json.load(answer)
    except Exception:
        return None

    return {
        "id": str(info.get("id", "")),
        "name": info.get("display_name") or info.get("real_name") or "пользователь",
    }


# ---------- сессии ----------
#
# Сессии живут в памяти процесса, а не в базе. Причина одна:
# в сессии лежит токен SourceCraft пользователя, а его не должно
# быть ни на диске, ни в логах. Перезапуск сервиса просто
# разлогинивает всех — для прототипа это правильный размен.

_sessions: dict[str, dict] = {}


def start_session(user: dict) -> str:
    session_id = secrets.token_urlsafe(32)
    _sessions[session_id] = {"user": user, "token": None, "touched": time.time()}
    return session_id


def session(session_id: str | None) -> dict | None:
    if not session_id:
        return None

    found = _sessions.get(session_id)
    if found is None:
        return None

    if time.time() - found["touched"] > SESSION_LIFETIME:
        _sessions.pop(session_id, None)
        return None

    found["touched"] = time.time()
    return found


def remember_sourcecraft_token(session_id: str, token: str):
    found = session(session_id)
    if found is not None:
        found["token"] = token


def forget(session_id: str | None):
    if session_id:
        _sessions.pop(session_id, None)
