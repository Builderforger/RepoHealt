using System.Text.Json;
using System.Text.Json.Serialization;
using RepoHealth.Collection;
using RepoHealth.Scoring;

// Движок оценки: принимает запрос на разбор репозитория,
// собирает факты и возвращает готовый отчёт.
// Сайту остаётся только показать результат.

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Иначе русские буквы в объяснениях уезжают в абв
    options.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    methodology = RepoHealthScore.MethodologyVersion
}));

app.MapPost("/analyze", async (
    AnalyzeRequest request,
    HttpRequest http,
    ILogger<Program> log,
    CancellationToken cancel) =>
{
    if (string.IsNullOrWhiteSpace(request.Repo) || !request.Repo.Contains('/'))
        return Results.BadRequest(new { error = "укажите репозиторий в виде организация/проект" });

    // Токен приходит заголовком от имени пользователя.
    // Если его нет — работаем служебным токеном сервиса.
    var token = http.Headers["X-SourceCraft-Token"].ToString();

    if (string.IsNullOrWhiteSpace(token))
        token = Environment.GetEnvironmentVariable("SC_TOKEN") ?? "";

    if (string.IsNullOrWhiteSpace(token))
        return Results.BadRequest(new { error = "нет токена доступа к SourceCraft" });

    var parts = request.Repo.Split('/', 2);
    var analyzedAt = DateTimeOffset.UtcNow;

    // В журнал пишем только имя проекта. Токен в логи попадать не должен.
    log.LogInformation("Разбираю {Repo}", request.Repo);

    using var collector = new FactsCollector(token);

    var facts = await collector.AnalyseAsync(parts[0], parts[1], analyzedAt, cancel);

    if (facts is null)
        return Results.NotFound(new { error = $"репозиторий {request.Repo} не найден или недоступен" });

    var report = RepoHealthScore.Evaluate(facts);
    report.AnalyzedAt = analyzedAt;

    return Results.Ok(report);
});

app.Run();

record AnalyzeRequest(string Repo);
