using System.Text.Json;
using RepoHealth.Contracts;

namespace RepoHealth.Scoring.Tests;

/// <summary>
/// Проверяет категорию «Документация».
/// </summary>
public class DocumentationCategoryTests
{
    // ---------- вспомогательное ----------

    /// Собирает факты прямо в памяти, без файлов:
    /// в тесте нужен не настоящий проект, а конкретная ситуация.
    private static RepoFacts Facts(string readme = "", params Note[] notes) => new()
    {
        Catalog = new Catalog { Name = "test/test", Url = "https://example.com" },
        Git = new Git { Readme = readme },
        Notes = [.. notes]
    };

    private static Note CloneFailed() => new()
    {
        Source = "git",
        Operation = "clone_depth_1",
        Reason = "repository not found"
    };

    private static Note ReadmeMissing() => new()
    {
        Source = "git",
        Operation = "readme",
        Reason = "README не найден"
    };

    private static PartResult PartNamed(CategoryResult result, string name) =>
        result.Parts.Single(part => part.Name == name);

    // ---------- главное: «нет данных» против «плохо» ----------

    [Fact]
    public void RepositoryNotClonedMeansNoData()
    {
        var result = DocumentationCategory.Evaluate(Facts(notes: CloneFailed()));

        Assert.Equal(CategoryStatus.NoData, result.Status);
        Assert.NotNull(result.NoDataReason);
        Assert.Empty(result.Parts);
    }

    [Fact]
    public void MissingReadmeIsScoredZeroNotNoData()
    {
        // Проект скачался, README у него действительно нет.
        // Это знание, а не незнание: ноль баллов, статус Scored.
        var result = DocumentationCategory.Evaluate(Facts(notes: ReadmeMissing()));

        Assert.Equal(CategoryStatus.Scored, result.Status);
        Assert.Equal(0, result.Score);
        Assert.NotEmpty(result.Parts);
    }

    // ---------- шкалы внутри категории ----------

    [Fact]
    public void HalfOfRequiredLinesGivesHalfOfVolumePoints()
    {
        // 60 непустых строк при потолке 120 — половина от 16 баллов
        var readme = string.Join("\n", Enumerable.Repeat("текст строки", 60));

        var volume = PartNamed(DocumentationCategory.Evaluate(Facts(readme)), "объём README");

        Assert.Equal(8, volume.Points, 1);
        Assert.Equal(16, volume.MaxPoints);
    }

    [Fact]
    public void HalfOfRequiredHeadingsGivesHalfOfStructurePoints()
    {
        // 6 заголовков при потолке 12 — половина от 15 баллов
        var readme = string.Join("\n", Enumerable.Range(1, 6).Select(n => $"# Глава {n}"));

        var structure = PartNamed(DocumentationCategory.Evaluate(Facts(readme)), "структура README");

        Assert.Equal(7.5, structure.Points, 1);
    }

    [Fact]
    public void EmptyReadmeScoresZero()
    {
        var result = DocumentationCategory.Evaluate(Facts(""));

        Assert.Equal(CategoryStatus.Scored, result.Status);
        Assert.Equal(0, result.Score);
        Assert.All(result.Parts, part => Assert.Equal(0, part.Points));
    }

    // ---------- разделы: заголовок против упоминания ----------

    [Fact]
    public void SectionInHeadingGivesFullPoints()
    {
        var result = DocumentationCategory.Evaluate(Facts("# Установка\nделай так"));

        Assert.Equal(18, PartNamed(result, "раздел про установку").Points, 1);
    }

    [Fact]
    public void MentionInTextGivesHalfPoints()
    {
        // слово есть, но отдельного раздела под него нет
        var result = DocumentationCategory.Evaluate(Facts("# Заголовок\nсначала build, потом всё"));

        Assert.Equal(9, PartNamed(result, "раздел про установку").Points, 1);
    }

    [Fact]
    public void NoMentionGivesZeroPoints()
    {
        var result = DocumentationCategory.Evaluate(Facts("# Заголовок\nобычный текст"));

        Assert.Equal(0, PartNamed(result, "раздел про установку").Points, 1);
    }

    [Theory]
    [InlineData("тут просто текст", 0)]
    [InlineData("вот код:\n```\nx = 1\n```", 6)]
    public void CodeBlocksArePaidForOnce(string readme, double expected)
    {
        Assert.Equal(expected, PartNamed(DocumentationCategory.Evaluate(Facts(readme)), "примеры кода").Points, 1);
    }

    // ---------- хороший README получает много ----------

    [Fact]
    public void GoodReadmeScoresHigh()
    {
        var readme = """
            # Проект

            ## Описание
            Делает полезное дело.

            ## Установка
            ```
            pip install project
            ```

            ## Использование
            ```
            project --help
            ```
            """;

        var result = DocumentationCategory.Evaluate(Facts(readme));

        // разделы и примеры кода взяты полностью
        Assert.Equal(18, PartNamed(result, "раздел про установку").Points, 1);
        Assert.Equal(12, PartNamed(result, "раздел про использование").Points, 1);
        Assert.Equal(6, PartNamed(result, "примеры кода").Points, 1);

        // а объёма и структуры не хватает — README короткий
        Assert.True(PartNamed(result, "объём README").Points < 20);
        Assert.True(result.Score > 45);
    }

    [Fact]
    public void BetterReadmeAlwaysScoresHigher()
    {
        // Ключевая проверка логики, а не чисел:
        // из двух README тот, что содержательнее, должен получить больше.
        var poor = DocumentationCategory.Evaluate(Facts("# Проект"));

        var rich = DocumentationCategory.Evaluate(Facts("""
            # Проект

            ## Установка
            Инструкция.

            ## Запуск
            ```
            run me
            ```
            """));

        Assert.True(rich.Score > poor.Score,
            $"богатый README набрал {rich.Score}, бедный {poor.Score}");
    }

    // ---------- общие свойства ----------

    [Fact]
    public void EveryPartExplainsItself()
    {
        // По ТЗ оценка должна опираться на проверяемые факты,
        // поэтому пустых объяснений быть не может.
        var result = DocumentationCategory.Evaluate(Facts("# Проект\nтекст"));

        Assert.All(result.Parts, part =>
            Assert.False(string.IsNullOrWhiteSpace(part.Explanation),
                $"часть «{part.Name}» без объяснения"));
    }

    [Fact]
    public void ScoreIsTheSumOfParts()
    {
        var result = DocumentationCategory.Evaluate(Facts("# Установка\n```\nкод\n```"));

        // балл категории — доля от того, что части могли дать вместе
        var possible = result.Parts.Sum(part => part.MaxPoints);
        var expected = result.Parts.Sum(part => part.Points) / possible * 100;

        Assert.Equal(expected, result.Score, 1);
    }

    // ---------- словари должны работать на обоих языках ----------

    [Theory]
    [InlineData("## Installation")]
    [InlineData("## Quick start")]
    [InlineData("## Download binary")]
    [InlineData("## Установка")]
    [InlineData("## Быстрый старт")]
    [InlineData("## Начало работы")]
    [InlineData("## Требования")]
    public void InstallSectionIsRecognisedInBothLanguages(string heading)
    {
        // На SourceCraft много русских README. Пока словарь был
        // только английским, раздел «Быстрый старт» не засчитывался,
        // и хорошие проекты теряли баллы ни за что.
        var result = DocumentationCategory.Evaluate(Facts($"# Проект\n\n{heading}\nтекст"));

        Assert.Equal(18, PartNamed(result, "раздел про установку").Points, 1);
    }

    [Theory]
    [InlineData("## Usage")]
    [InlineData("## Examples")]
    [InlineData("## Использование")]
    [InlineData("## Запуск")]
    [InlineData("## Возможности")]
    [InlineData("## Примеры")]
    public void UsageSectionIsRecognisedInBothLanguages(string heading)
    {
        var result = DocumentationCategory.Evaluate(Facts($"# Проект\n\n{heading}\nтекст"));

        Assert.Equal(12, PartNamed(result, "раздел про использование").Points, 1);
    }

    // ---------- проверка на настоящих данных ----------

    [Fact]
    public void AllFixturesAreScoredWithoutErrors()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "fixtures");
        var problems = new List<string>();

        foreach (var file in Directory.GetFiles(folder, "*.json"))
        {
            var facts = JsonSerializer.Deserialize<RepoFacts>(File.ReadAllText(file))!;
            var result = DocumentationCategory.Evaluate(facts);

            if (result.Status == CategoryStatus.NoData)
                continue;

            if (result.Score is < 0 or > 100)
                problems.Add($"{Path.GetFileName(file)}: балл вне диапазона — {result.Score}");
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
