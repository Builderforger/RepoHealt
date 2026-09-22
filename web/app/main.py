"""Веб-сервис оценки здоровья открытых репозиториев SourceCraft."""

from pathlib import Path

from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import HTMLResponse, PlainTextResponse
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates

from . import db, markdown_report

HERE = Path(__file__).parent
PER_PAGE = 50

app = FastAPI(title="SourceCraft Repo Health")
app.mount("/static", StaticFiles(directory=HERE / "static"), name="static")

templates = Jinja2Templates(directory=HERE / "templates")


@app.on_event("startup")
def startup():
    db.prepare()


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
