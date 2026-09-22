using System.Text;
using System.Text.Json;
using RepoHealth.Collection;
using RepoHealth.Contracts;
using RepoHealth.Scoring;

// Чтобы русские буквы не превращались в кракозябры в консоли Windows.
Console.OutputEncoding = Encoding.UTF8;

if (args.Length == 0)
{
    Console.WriteLine("""
        Сбор фактов о репозиториях SourceCraft и расчёт оценки.

          repohealth collect <папка> [--limit N]  собрать факты в папку
          repohealth <файл.json>                  разбор одного проекта
          repohealth <папка>                      таблица по всем файлам папки
          repohealth <файл.json> --json           отчёт в машиночитаемом виде

        Для сбора нужен токен SourceCraft в переменной окружения SC_TOKEN.
        """);
    return 1;
}

if (args[0] == "collect")
    return await CollectAsync(args);

var path = args[0];
var asJson = args.Contains("--json");

if (Directory.Exists(path))
{
    PrintTable(path);
    return 0;
}

if (!File.Exists(path))
{
    Console.Error.WriteLine($"Файл не найден: {path}");
    return 1;
}

var report = Evaluate(path);

if (asJson)
{
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    }));
}
else
{
    PrintReport(report);
}

return 0;


static async Task<int> CollectAsync(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Укажите папку: repohealth collect <папка> [--limit N]");
        return 1;
    }

    var token = Environment.GetEnvironmentVariable("SC_TOKEN");
    if (string.IsNullOrWhiteSpace(token))
    {
        Console.Error.WriteLine("Нет токена. Задайте переменную окружения SC_TOKEN.");
        return 1;
    }

    var folder = args[1];
    var limit = 0;

    var limitAt = Array.IndexOf(args, "--limit");
    if (limitAt >= 0 && limitAt + 1 < args.Length)
        int.TryParse(args[limitAt + 1], out limit);

    // Момент сбора фиксируем один раз на весь проход,
    // чтобы «дней назад» у всех репозиториев считалось от одной точки.
    var startedAt = DateTimeOffset.UtcNow;

    using var collector = new FactsCollector(token);

    var progress = new Progress<string>(message =>
        Console.WriteLine("  " + message));

    Console.WriteLine(limit > 0
        ? $"Собираю факты по {limit} репозиториям в {folder}"
        : $"Собираю факты по всему каталогу в {folder}");

    var saved = await collector.CollectToFolderAsync(folder, limit, startedAt, progress);

    Console.WriteLine();
    Console.WriteLine($"Готово: сохранено {saved} файлов, время сбора {startedAt:u}");
    return 0;
}

static HealthReport Evaluate(string file)
{
    var facts = JsonSerializer.Deserialize<RepoFacts>(File.ReadAllText(file))
                ?? throw new InvalidOperationException($"не удалось прочитать {file}");

    return RepoHealthScore.Evaluate(facts);
}

static void PrintReport(HealthReport report)
{
    var score = report.Score is null ? "нет оценки" : $"{report.Score:0.#} / 100";

    Console.WriteLine();
    Console.WriteLine($"{report.Repo}   {score}");
    Console.WriteLine(report.Url);
    Console.WriteLine($"методика {report.MethodologyVersion}");
    Console.WriteLine();

    foreach (var category in report.Categories.Where(c => c.Status == CategoryStatus.Scored))
    {
        Console.WriteLine($"  {category.Name,-18} {category.Score,5:0.#}   вес {category.AppliedWeight:0.#}%");

        foreach (var part in category.Parts)
        {
            Console.WriteLine(
                $"      {part.Name,-16} {part.Points,5:0.#}/{part.MaxPoints,-4:0.#} {part.Explanation}");
        }

        Console.WriteLine();
    }

    var missing = report.Categories.Where(c => c.Status == CategoryStatus.NoData).ToList();
    if (missing.Count == 0)
        return;

    Console.WriteLine("  Не посчитано:");
    foreach (var category in missing)
        Console.WriteLine($"      {category.Name,-18} {category.NoDataReason}");

    Console.WriteLine();
}

static void PrintTable(string folder)
{
    var rows = Directory.GetFiles(folder, "*.json")
        .Select(Evaluate)
        .OrderByDescending(report => report.Score ?? -1)
        .ToList();

    Console.WriteLine();
    Console.WriteLine($"  {"итог",6}  {"проект",-46} {"категории",-9}");
    Console.WriteLine("  " + new string('-', 66));

    foreach (var report in rows)
    {
        var score = report.Score is null ? "  —" : $"{report.Score,6:0.#}";
        var scored = report.Categories.Count(c => c.Status == CategoryStatus.Scored);

        Console.WriteLine($"  {score}  {Shorten(report.Repo, 46),-46} {scored} из 6");
    }

    var withScore = rows.Where(r => r.Score is not null).ToList();

    Console.WriteLine();
    Console.WriteLine($"  всего проектов: {rows.Count}, с оценкой: {withScore.Count}");

    if (withScore.Count > 0)
        Console.WriteLine($"  средний балл: {withScore.Average(r => r.Score!.Value):0.#}");

    Console.WriteLine();
}

static string Shorten(string text, int limit) =>
    text.Length <= limit ? text : text[..(limit - 1)] + "…";
