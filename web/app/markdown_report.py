"""Отчёт в Markdown — то, что пользователь скачивает со страницы проекта."""


def render(report: dict) -> str:
    score = report.get("score")
    score_text = f"{score:.0f} / 100" if score is not None else "оценить не удалось"

    lines = [
        f"# {report.get('repo', '')}",
        "",
        f"**Оценка здоровья: {score_text}**",
        "",
        f"- Репозиторий: {report.get('url', '')}",
    ]

    if report.get("language"):
        lines.append(f"- Основной язык: {report['language']}")

    if report.get("likes") is not None:
        lines.append(f"- Рейтинг лайков: {report['likes']:.0f}")

    if report.get("last_activity"):
        lines.append(f"- Последняя активность: {report['last_activity'][:10]}")

    if report.get("analyzed_at"):
        lines.append(f"- Дата анализа: {report['analyzed_at'][:10]}")

    lines += [
        f"- Версия методики: {report.get('methodology_version', '')}",
        "",
        "## Оценка по категориям",
        "",
        "| Категория | Балл | Вес в итоге |",
        "| --- | --- | --- |",
    ]

    scored = [c for c in report.get("categories", []) if c.get("status") == "Scored"]
    missing = [c for c in report.get("categories", []) if c.get("status") != "Scored"]

    for category in scored:
        lines.append(
            f"| {category['name']} | {category['score']:.0f} | "
            f"{category.get('applied_weight', 0):.0f}% |"
        )

    for category in scored:
        lines += ["", f"### {category['name']}", ""]

        for part in category.get("parts", []):
            lines.append(
                f"- **{part['name']}** — {part['points']:.0f} из "
                f"{part['max_points']:.0f}: {part['explanation']}"
            )

    if missing:
        lines += ["", "## Нет данных", ""]
        lines.append(
            "Эти категории не участвовали в расчёте. "
            "Их вес распределён между остальными, "
            "отсутствие данных не снижает оценку."
        )
        lines.append("")

        for category in missing:
            lines.append(f"- **{category['name']}** — {category.get('no_data_reason', '')}")

    lines += [
        "",
        "---",
        "",
        "Отчёт сформирован сервисом оценки здоровья репозиториев SourceCraft.",
        "",
    ]

    return "\n".join(lines)
