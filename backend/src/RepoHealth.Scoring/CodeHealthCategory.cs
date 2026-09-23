using RepoHealth.Contracts;

namespace RepoHealth.Scoring;

/// Оценивает состояние кода: есть ли тесты, много ли недоделок,
/// настроена ли проверка кода и нет ли разросшихся файлов.
public static class CodeHealthCategory
{
    public const string Name = "Состояние кода";
    public const double BaseWeight = 20;

    /// Одна недоделка на сорок файлов — терпимо.
    /// Одна на четыре — проект тонет в долге.
    private const double GoodTodoDensity = 0.025;
    private const double BadTodoDensity = 0.25;

    /// Файл на сто килобайт ещё читаем, на мегабайт — уже нет.
    private const long GoodFileSize = 100 * 1024;
    private const long BadFileSize = 1024 * 1024;

    public static CategoryResult Evaluate(RepoFacts facts)
    {
        var failure = CategoryTools.FindCollectionFailure(facts);
        if (failure is not null)
        {
            return CategoryResult.NotEnoughData(
                Name, BaseWeight, $"не удалось скачать репозиторий: {failure.Reason}");
        }

        var code = facts.Code;

        if (code is null || code.FilesTotal == 0)
        {
            return CategoryResult.NotEnoughData(
                Name, BaseWeight, "дерево файлов прочитать не удалось");
        }

        var density = (double)code.TodoCount / code.FilesTotal;

        var parts = new List<PartResult>
        {
            CategoryTools.Part("тесты", 30, code.HasTests ? 1 : 0,
                code.HasTests
                    ? "в проекте есть файлы тестов"
                    : "файлов тестов не нашлось"),

            CategoryTools.Part("недоделки в коде", 30,
                Between(density, GoodTodoDensity, BadTodoDensity),
                code.TodoCount == 0
                    ? $"пометок TODO и FIXME нет ({code.FilesTotal} файлов)"
                    : $"{code.TodoCount} пометок TODO и FIXME на {code.FilesTotal} файлов"),

            CategoryTools.Part("проверка кода настроена", 20,
                code.QualityConfig is null ? 0 : 1,
                code.QualityConfig is null
                    ? "настроек линтера или форматтера не нашлось"
                    : $"найдено: {code.QualityConfig}"),

            CategoryTools.Part("нет разросшихся файлов", 20,
                Between(code.LargestFileBytes, GoodFileSize, BadFileSize),
                $"самый большой файл — {Readable(code.LargestFileBytes)}")
        };

        return new CategoryResult
        {
            Name = Name,
            Status = CategoryStatus.Scored,
            BaseWeight = BaseWeight,
            Score = CategoryTools.Combine(parts),
            Parts = parts
        };
    }

    /// Единица пока значение не хуже «хорошего», ноль после «плохого»,
    /// между ними ровный спуск. То же, что Fades, но для любых величин.
    private static double Between(double value, double good, double bad)
    {
        if (value <= good)
            return 1;

        if (value >= bad)
            return 0;

        return 1 - (value - good) / (bad - good);
    }

    private static string Readable(long bytes) =>
        bytes >= 1024 * 1024
            ? $"{bytes / 1024.0 / 1024:0.#} МБ"
            : $"{bytes / 1024.0:0} КБ";
}
