# Движок оценки: HTTP-служба плюс тот же расчёт в виде команды.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/ ./

RUN dotnet publish src/RepoHealth.Api/RepoHealth.Api.csproj -c Release -o /out/api \
 && dotnet publish src/RepoHealth.Cli/RepoHealth.Cli.csproj -c Release -o /out/cli

FROM mcr.microsoft.com/dotnet/aspnet:10.0

# git нужен по-настоящему: движок скачивает репозитории,
# чтобы прочитать README и историю коммитов.
RUN apt-get update \
 && apt-get install -y --no-install-recommends git ca-certificates \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /out ./

# Папка для собранных фактов и отчётов, общая с сайтом.
RUN mkdir -p /data
VOLUME ["/data"]

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_gcServer=1

EXPOSE 8080

# Служба запускается по умолчанию. Разовый сбор запускается так:
#   docker compose run --rm --entrypoint dotnet engine \
#       /app/cli/RepoHealth.Cli.dll collect /data/facts
ENTRYPOINT ["dotnet", "/app/api/RepoHealth.Api.dll"]
