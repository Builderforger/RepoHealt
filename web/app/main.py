"""Веб-сервис оценки здоровья открытых репозиториев SourceCraft."""

import asyncio
import secrets
from pathlib import Path

from fastapi import FastAPI, Form, HTTPException, Request
from fastapi.responses import HTMLResponse, PlainTextResponse, RedirectResponse
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates

from . import auth, db, markdown_report, refresh, sourcecraft

SESSION_COOKIE = "repohealth_session"

HERE = Path(__file__).parent
PER_PAGE = 50

app = FastAPI(title="SourceCraft Repo Health")
app.mount("/static", StaticFiles(directory=HERE / "static"), name="static")

templates = Jinja2Templates(directory=HERE / "templates")


# Ссылку на фоновую задачу держим сами: без неё сборщик мусора
# вправе убрать задачу прямо посреди работы, и слежение за
# отчётами тихо прекратится.
_watcher: asyncio.Task | None = None


@app.on_event("startup")
async def startup():
    global _watcher

    db.prepare()

    # Отчёты готовит движок по расписанию. Сайт следит
    # за файлом и переливает свежие данные в свою базу.
    _watcher = asyncio.create_task(refresh.watch())


@app.get("/", response_class=HTMLResponse)
def rating_page(request: Request, language: str = "", sort: str = "score", page: int = 1):
    rows, total = db.rating(
        language=language or None, sort=sort, page=page, per_page=PER_PAGE
    )

    pages = max((total + PER_PAGE - 1) // PER_PAGE, 1)

    return templates.TemplateResponse(
        "rating.html",
        {
            "request": request,
            "rows": rows,
            "total": total,
            "page": page,
            "pages": pages,
            "first_place": (page - 1) * PER_PAGE + 1,
            "language": language,
            "sort": sort,
            "languages": db.languages(),
            "summary": db.summary(),
            "run": refresh.status(),
        },
    )


@app.get("/repo/{org}/{name}", response_class=HTMLResponse)
def repo_page(request: Request, org: str, name: str):
    report = db.report(f"{org}/{name}")

    if report is None:
        raise HTTPException(status_code=404, detail="Такой проект ещё не анализировался")

    categories = report.get("categories", [])

    return templates.TemplateResponse(
        "repo.html",
        {
            "request": request,
            "report": report,
            "scored": [c for c in categories if c.get("status") == "Scored"],
            "missing": [c for c in categories if c.get("status") != "Scored"],
        },
    )


@app.get("/repo/{org}/{name}/report.md", response_class=PlainTextResponse)
def repo_markdown(org: str, name: str):
    report = db.report(f"{org}/{name}")

    if report is None:
        raise HTTPException(status_code=404, detail="Такой проект ещё не анализировался")

    return PlainTextResponse(
        markdown_report.render(report),
        media_type="text/markdown; charset=utf-8",
        headers={"Content-Disposition": f'attachment; filename="{org}-{name}-health.md"'},
    )


# ---------- личный кабинет ----------

def current(request: Request) -> dict | None:
    return auth.session(request.cookies.get(SESSION_COOKIE))


@app.get("/login")
def login(request: Request):
    if not auth.configured():
        return templates.TemplateResponse(
            "notice.html",
            {
                "request": request,
                "title": "Вход пока не настроен",
                "text": "Личный кабинет работает через Яндекс ID. "
                        "Чтобы его включить, нужно зарегистрировать приложение "
                        "и задать две переменные окружения.",
                "steps": [
                    "Зарегистрировать приложение на oauth.yandex.ru/client/new",
                    "Разрешить доступ к логину и почте пользователя",
                    "Указать адрес возврата: " + auth.REDIRECT_URI,
                    "Задать переменные YANDEX_CLIENT_ID и YANDEX_CLIENT_SECRET",
                    "Перезапустить сервис",
                ],
            },
            status_code=503)

    return RedirectResponse(auth.login_url(state=secrets.token_urlsafe(16)))


@app.get("/auth/callback")
def auth_callback(code: str = "", error: str = ""):
    if error or not code:
        return RedirectResponse("/?login=failed")

    yandex_token = auth.exchange_code(code)
    if yandex_token is None:
        return RedirectResponse("/?login=failed")

    user = auth.who_is_it(yandex_token)
    if user is None:
        return RedirectResponse("/?login=failed")

    session_id = auth.start_session(user)

    answer = RedirectResponse("/cabinet")
    answer.set_cookie(
        SESSION_COOKIE, session_id,
        httponly=True, samesite="lax", secure=auth.secure_cookies())
    return answer


@app.get("/logout")
def logout(request: Request):
    auth.forget(request.cookies.get(SESSION_COOKIE))

    answer = RedirectResponse("/")
    answer.delete_cookie(SESSION_COOKIE)
    return answer


@app.get("/cabinet", response_class=HTMLResponse)
def cabinet(request: Request, message: str = ""):
    session = current(request)

    if session is None:
        return RedirectResponse("/login")

    repositories = None
    if session.get("token"):
        repositories = sourcecraft.my_repositories(session["token"])
        if repositories is None:
            message = message or "Токен не подошёл: SourceCraft не отдал список репозиториев"

    return templates.TemplateResponse(
        "cabinet.html",
        {
            "request": request,
            "user": session["user"],
            "has_token": bool(session.get("token")),
            "repositories": repositories,
            "analysed": db.personal_reports(session["user"]["id"]),
            "message": message,
        },
    )


@app.post("/cabinet/token")
def save_token(request: Request, token: str = Form("")):
    session_id = request.cookies.get(SESSION_COOKIE)

    if auth.session(session_id) is None:
        return RedirectResponse("/login", status_code=303)

    auth.remember_sourcecraft_token(session_id, token.strip())
    return RedirectResponse("/cabinet", status_code=303)


@app.get("/cabinet/token-reset")
def reset_token(request: Request):
    session_id = request.cookies.get(SESSION_COOKIE)
    auth.remember_sourcecraft_token(session_id or "", "")
    return RedirectResponse("/cabinet")


@app.post("/cabinet/analyse")
def analyse_my_repo(request: Request, repo: str = Form("")):
    session = current(request)

    if session is None:
        return RedirectResponse("/login", status_code=303)

    if not session.get("token"):
        return RedirectResponse("/cabinet?message=Сначала укажите токен", status_code=303)

    repo = repo.strip().strip("/")

    # Имя приходит в том числе из поля ввода, а проверке в браузере
    # доверять нельзя. Ждём ровно «владелец/название».
    parts = repo.split("/")

    if len(parts) != 2 or not all(parts):
        return RedirectResponse(
            "/cabinet?message=Укажите репозиторий в виде владелец/название",
            status_code=303)

    report = sourcecraft.analyse(repo, session["token"])

    if report is None:
        return RedirectResponse(
            f"/cabinet?message=Не удалось разобрать {repo}", status_code=303)

    db.save_personal(session["user"]["id"], report)

    return RedirectResponse(f"/cabinet/report/{repo}", status_code=303)


@app.get("/cabinet/report/{org}/{name}", response_class=HTMLResponse)
def personal_report(request: Request, org: str, name: str):
    session = current(request)

    if session is None:
        return RedirectResponse("/login")

    report = db.personal_report(session["user"]["id"], f"{org}/{name}")

    if report is None:
        raise HTTPException(status_code=404, detail="Такой разбор не найден")

    categories = report.get("categories", [])

    return templates.TemplateResponse(
        "repo.html",
        {
            "request": request,
            "report": report,
            "personal": True,
            "scored": [c for c in categories if c.get("status") == "Scored"],
            "missing": [c for c in categories if c.get("status") != "Scored"],
        },
    )


# ---------- машиночитаемое ----------

@app.get("/api/repos")
def api_rating(language: str = "", sort: str = "score", page: int = 1):
    """Публичный доступ к рейтингу для тех, кому нужен не сайт, а данные."""
    rows, total = db.rating(language=language or None, sort=sort, page=page, per_page=PER_PAGE)
    return {"total": total, "page": page, "repositories": rows}


@app.get("/api/repos/{org}/{name}")
def api_report(org: str, name: str):
    report = db.report(f"{org}/{name}")

    if report is None:
        raise HTTPException(status_code=404, detail="Такой проект ещё не анализировался")

    return report
