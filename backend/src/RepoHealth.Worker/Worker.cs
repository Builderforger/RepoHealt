using System.Text;
using System.Text.Json;
using RepoHealth.Collection;
using RepoHealth.Contracts;
using RepoHealth.Scoring;

namespace RepoHealth.Worker;

/// Регулярно обходит каталог и пересчитывает оценки.
///
/// Обход инкрементальный: заново скачиваются только те проекты,
/// которые менялись с прошлого раза. Поэтому первый проход долгий,
/// а каждый следующий — короткий.
public class Worker(ILogger<Worker> log, IConfiguration settings) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        var token = settings["SC_TOKEN"] ?? "";
        var dataFolder = settings["REPOHEALTH_DATA"] ?? "/data";
        var hours = double.TryParse(settings["REPOHEALTH_INTERVAL_HOURS"], out var value) ? value : 6;
        var limit = int.TryParse(settings["REPOHEALTH_LIMIT"], out var count) ? count : 0;

        if (string.IsNullOrWhiteSpace(token))
        {
            log.LogWarning(
                "Нет токена SourceCraft — периодический пересчёт выключен. " +
                "Задайте переменную SC_TOKEN, чтобы он заработал.");
            return;
        }

        var interval = TimeSpan.FromHours(hours);
        log.LogInformation("Пересчёт запускается раз в {Hours} ч.", hours);

        while (!stopping.IsCancellationRequested)
        {
            await RunOnceAsync(token, dataFolder, limit, stopping);

            log.LogInformation("Следующий проход через {Hours} ч.", hours);

            try
            {
                await Task.Delay(interval, stopping);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(
        string token, string dataFolder, int limit, CancellationToken stopping)
    {
        var factsFolder = Path.Combine(dataFolder, "facts");
        var reportsFile = Path.Combine(dataFolder, "reports.jsonl");
        var statusFile = Path.Combine(dataFolder, "last-run.json");

        var run = new CollectionRun { StartedAt = DateTimeOffset.UtcNow };
        await SaveStatusAsync(statusFile, run, stopping);

        try
        {
            using var collector = new FactsCollector(token);

            var progress = new Progress<string>(message => log.LogInformation("{Message}", message));

            run = await collector.CollectToFolderAsync(
                factsFolder, limit, run.StartedAt, force: false, progress, stopping);

            var written = WriteReports(factsFolder, reportsFile, run.StartedAt);

            log.LogInformation(
                "Проход закончен: всего {Total}, заново {Collected}, пропущено {Skipped}, " +
                "ошибок {Failed}, отчётов {Reports}, заняло {Duration}",
                run.Total, run.Collected, run.Skipped, run.Failed, written, run.Duration);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            run.Status = "failed";
            run.Error = error.Message;
            run.FinishedAt = DateTimeOffset.UtcNow;

            log.LogError(error, "Проход не удался");
        }

        await SaveStatusAsync(statusFile, run, stopping);
    }

    /// Считает отчёты по собранным фактам. Пересчитываются все,
    /// даже непересобранные: методика могла измениться.
    private static int WriteReports(string factsFolder, string reportsFile, DateTimeOffset analyzedAt)
    {
        if (!Directory.Exists(factsFolder))
            return 0;

        var temporary = reportsFile + ".tmp";
        var written = 0;

        using (var output = new StreamWriter(temporary, append: false, new UTF8Encoding(false)))
        {
            foreach (var file in Directory.EnumerateFiles(factsFolder, "*.json"))
            {
                RepoFacts facts;
                try
                {
                    facts = FactsJson.Read(File.ReadAllText(file));
                }
                catch
                {
                    continue;
                }

                var report = RepoHealthScore.Evaluate(facts);
                report.AnalyzedAt = analyzedAt;

                output.WriteLine(JsonSerializer.Serialize(report, ReportJson.Line));
                written++;
            }
        }

        // Подменяем файл целиком: сайт не должен прочитать половину.
        File.Move(temporary, reportsFile, overwrite: true);

        return written;
    }

    private static async Task SaveStatusAsync(
        string statusFile, CollectionRun run, CancellationToken cancel)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(statusFile)!);
            await File.WriteAllTextAsync(
                statusFile, JsonSerializer.Serialize(run, ReportJson.Readable), cancel);
        }
        catch (Exception error)
        {
            // Не смогли записать статус — работу из-за этого не останавливаем.
            Console.Error.WriteLine($"не удалось записать статус: {error.Message}");
        }
    }
}
