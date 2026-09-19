using System.Text.Json;
using RepoHealth.Contracts;

namespace RepoHealth.Scoring.Tests;

/// <summary>
/// Проверяет, что файлы фикстур читаются в наши классы.
/// Ловит несовпадение имён полей: System.Text.Json на них
/// не ругается, а молча оставляет поля пустыми.
/// </summary>
public class FactsLoadingTests
{
    // Папка fixtures лежит рядом с собранным тестом —
    // её кладёт туда csproj при сборке.
    private static string FixturesFolder =>
        Path.Combine(AppContext.BaseDirectory, "fixtures");

    [Fact]
    public void FilesAreInPlace()
    {
        Assert.True(
            Directory.Exists(FixturesFolder),
            $"Папки с фикстурами нет: {FixturesFolder}");

        var files = Directory.GetFiles(FixturesFolder, "*.json");

        Assert.True(
            files.Length > 0,
            $"В папке {FixturesFolder} нет ни одного файла .json");
    }

    [Fact]
    public void EveryFixtureIsReadAndFilled()
    {
        var problems = new List<string>();

        foreach (var file in Directory.GetFiles(FixturesFolder, "*.json"))
        {
            var shortName = Path.GetFileName(file);

            RepoFacts? facts;
            try
            {
                facts = JsonSerializer.Deserialize<RepoFacts>(File.ReadAllText(file));
            }
            catch (Exception error)
            {
                problems.Add($"{shortName}: не разобрался — {error.Message}");
                continue;
            }

            if (facts is null)
            {
                problems.Add($"{shortName}: получился null");
                continue;
            }

            if (facts.Catalog is null)
            {
                problems.Add($"{shortName}: нет блока catalog");
                continue;
            }

            if (string.IsNullOrWhiteSpace(facts.Catalog.Name))
            {
                problems.Add($"{shortName}: пустое название проекта");
            }

            if (string.IsNullOrWhiteSpace(facts.Catalog.Url))
            {
                problems.Add($"{shortName}: пустая ссылка");
            }

            if (facts.Git is null)
            {
                problems.Add($"{shortName}: нет блока git");
            }
        }

        Assert.True(
            problems.Count == 0,
            "Проблемы при чтении фикстур:\n" + string.Join("\n", problems));
    }
}
