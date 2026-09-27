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

    // Речь только о файлах, которые пишут руками.
    // Сто килобайт кода — это примерно три тысячи строк:
    // много, но жить можно. Полмегабайта в одном файле — уже беда.
    private const long GoodFileSize = 100 * 1024;
    private const long BadFileSize = 500 * 1024;

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
            CategoryTools.Part("тесты", 35, code.HasTests ? 1 : 0,
                code.HasTests
                    ? "в проекте есть файлы тестов"
                    : "файлов тестов не нашлось"),

            CategoryTools.Part("недоделки в коде", 30,
                Between(density, GoodTodoDensity, BadTodoDensity),
                code.TodoCount == 0
                    ? $"пометок TODO и FIXME нет; файлов в проекте: {code.FilesTotal}"
                    : $"пометок TODO и FIXME: {code.TodoCount}; файлов в проекте: {code.FilesTotal}"),

            CategoryTools.Part("настройки проверки кода", 20,
                code.QualityConfig is null ? 0 : 1,
                code.QualityConfig is null
                    ? "настроек линтера или форматтера не нашлось"
                    : $"найдено: {code.QualityConfig}"),

            SourceFileSize(code)
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

    /// Размер самого большого файла с кодом.
    /// Если таких файлов не нашлось вовсе — например, репозиторий
    /// со словарями или вёрсткой — часть выпадает, а не обнуляется.
    private static PartResult SourceFileSize(CodeFacts code)
    {
        if (code.LargestSourceFileBytes == 0)
        {
            return CategoryTools.Part("размер файлов кода", 15, 1,
                "файлов с исходным кодом не нашлось, размер оценивать не по чему");
        }

        var where = code.LargestSourceFile is null ? "" : $" — {code.LargestSourceFile}";

        return CategoryTools.Part("размер файлов кода", 15,
            Between(code.LargestSourceFileBytes, GoodFileSize, BadFileSize),
            $"самый большой файл с кодом {Readable(code.LargestSourceFileBytes)}{where} " +
            $"(до {Readable(GoodFileSize)} это норма)");
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

    private static string Readable(long bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / 1024.0 / 1024:0.#} МБ";

        // Меньше килобайта округлялось до «0 КБ» — на репозитории
        // из почти пустых файлов это выглядело как ошибка.
        if (bytes < 1024)
            return $"{bytes} Б";

        return $"{bytes / 1024.0:0} КБ";
    }
}
