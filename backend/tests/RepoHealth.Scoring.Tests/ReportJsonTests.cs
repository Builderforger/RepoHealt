using System.Text.Json;
using RepoHealth.Contracts;

namespace RepoHealth.Scoring.Tests;

/// Файл отчётов сайт читает построчно. Если отчёт запишется
/// с переносами, загрузка молча даст ноль записей — так уже
/// случилось на полном обходе каталога.
public class ReportJsonTests
{
    private static HealthReport AnyReport()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "fixtures");
        var file = Directory.GetFiles(folder, "*.json").First();
        var facts = JsonSerializer.Deserialize<RepoFacts>(File.ReadAllText(file))!;

        return RepoHealthScore.Evaluate(facts);
    }

    [Fact]
    public void ОтчётДляФайлаЗанимаетОднуСтроку()
    {
        var written = JsonSerializer.Serialize(AnyReport(), ReportJson.Line);

        Assert.DoesNotContain('\n', written);
        Assert.DoesNotContain('\r', written);
    }

    [Fact]
    public void ВсеОтчётыЗаписываютсяПоСтрокеНаКаждый()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "fixtures");
        var files = Directory.GetFiles(folder, "*.json");

        var lines = files
            .Select(file => JsonSerializer.Deserialize<RepoFacts>(File.ReadAllText(file))!)
            .Select(facts => JsonSerializer.Serialize(RepoHealthScore.Evaluate(facts), ReportJson.Line))
            .ToList();

        Assert.Equal(files.Length, lines.Count);
        Assert.All(lines, line => Assert.DoesNotContain('\n', line));
    }

    [Fact]
    public void ОтчётДляЧеловекаРазбитНаСтроки()
    {
        var shown = JsonSerializer.Serialize(AnyReport(), ReportJson.Readable);

        Assert.Contains('\n', shown);
    }

    [Fact]
    public void РусскийТекстНеПревращаетсяВКоды()
    {
        var written = JsonSerializer.Serialize(AnyReport(), ReportJson.Line);

        Assert.Contains("Документация", written);
    }
}
