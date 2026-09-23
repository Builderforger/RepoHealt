# Сайт: рейтинг, страницы проектов и личный кабинет.

FROM python:3.13-slim

WORKDIR /app

COPY web/requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt

COPY web/app ./app

# База и общая папка с отчётами лежат снаружи образа,
# чтобы переживать пересборку.
ENV REPOHEALTH_DB=/data/repohealth.db \
    PYTHONUNBUFFERED=1

RUN mkdir -p /data
VOLUME ["/data"]

EXPOSE 8000

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
