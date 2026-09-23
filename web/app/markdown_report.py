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

    recommendations = report.get("recommendations") or []

    if recommendations:
        titles = {"High": "Высокий приоритет",
                  "Medium": "Средний приоритет",
                  "Low": "Низкий приоритет"}

        lines += ["", "## Что улучшить", ""]

        for number, item in enumerate(recommendations, start=1):
            priority = titles.get(item.get("priority", ""), item.get("priority", ""))

            lines += [
                f"### {number}. {item['problem']}",
                "",
                f"- **Приоритет:** {priority}",
                f"- **Категория:** {item['category']}",
                f"- **Почему важно:** {item['why']}",
                f"- **На чём основано:** {item['evidence']}",
                f"- **Что сделать:** {item['action']}",
                f"- **Ожидаемый эффект:** +{item['expected_gain']:.1f} балла к итоговой оценке",
                "",
            ]

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
