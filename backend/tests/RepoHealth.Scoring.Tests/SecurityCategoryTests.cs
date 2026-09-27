using RepoHealth.Contracts;

namespace RepoHealth.Scoring.Tests;

public class SecurityCategoryTests
{
    private static RepoFacts With(SecurityFacts? security) =>
        new() { Catalog = new Catalog(), Git = new Git(), Security = security };

    private static SecurityFacts Clean(string? finished = null) => new()
    {
        ScanFound = true,
        ScanFinishedAt = finished ?? DateTimeOffset.UtcNow.ToString("O"),
        Critical = 0, High = 0, Medium = 0, Low = 0, Total = 0
    };

    // ---------- когда оценивать нечего ----------

    [Fact]
    public void БезДоступаКПроверкамЭтоНеПлохаяОценка()
    {
        var result = SecurityCategory.Evaluate(With(null));

        Assert.Equal(CategoryStatus.NoData, result.Status);
        Assert.Contains("участникам репозитория", result.NoDataReason);
    }

    [Fact]
    public void ПроверкаНиРазуНеОтработалаТожеНеПлохо()
    {
        var result = SecurityCategory.Evaluate(With(new SecurityFacts { ScanFound = false }));

        Assert.Equal(CategoryStatus.NoData, result.Status);
        Assert.Contains("ни разу", result.NoDataReason);
    }

    [Fact]
    public void ПроверкаБезРазбивкиПоУровнямНеОценивается()
    {
        var facts = With(new SecurityFacts
        {
            ScanFound = true,
            ScanFinishedAt = DateTimeOffset.UtcNow.ToString("O"),
            Critical = null, High = null, Medium = null
        });

        var result = SecurityCategory.Evaluate(facts);

        Assert.Equal(CategoryStatus.NoData, result.Status);
    }

    // ---------- сама оценка ----------

    [Fact]
    public void ЧистаяСвежаяПроверкаДаётСотню()
    {
        var result = SecurityCategory.Evaluate(With(Clean()));

        Assert.Equal(CategoryStatus.Scored, result.Status);
        Assert.Equal(100, result.Score);
    }

    [Fact]
    public void ДвеКритическиеНаходкиЗабираютВсюСвоюЧасть()
    {
        var security = Clean();
        security.Critical = 2;

        var result = SecurityCategory.Evaluate(With(security));

        var critical = result.Parts.Single(part => part.Name == "критические находки");
        Assert.Equal(0, critical.Points);
    }

    [Fact]
    public void ОднаКритическаяХужеЧемДесятьСредних()
    {
        var one = Clean();
        one.Critical = 1;

        var many = Clean();
        many.Medium = 10;

        var withCritical = SecurityCategory.Evaluate(With(one)).Score;
        var withMedium = SecurityCategory.Evaluate(With(many)).Score;

        Assert.True(withCritical < withMedium,
            $"критическая: {withCritical}, средние: {withMedium}");
    }

    [Fact]
    public void ЧемБольшеНаходокТемНижеОценка()
    {
        var few = Clean();
        few.High = 1;

        var more = Clean();
        more.High = 4;

        Assert.True(SecurityCategory.Evaluate(With(more)).Score
                  < SecurityCategory.Evaluate(With(few)).Score);
    }

    [Fact]
    public void УровеньКоторыйApiНеОтдалНеОбнуляетКатегорию()
    {
        var partial = Clean();
        partial.Medium = null;

        var result = SecurityCategory.Evaluate(With(partial));

        Assert.Equal(100, result.Score);
        Assert.DoesNotContain(result.Parts, part => part.Name == "средние находки");
    }

    [Fact]
    public void СтараяПроверкаСнижаетОценку()
    {
        var old = Clean(DateTimeOffset.UtcNow.AddDays(-300).ToString("O"));

        var result = SecurityCategory.Evaluate(With(old));

        var freshness = result.Parts.Single(part => part.Name == "свежесть проверки");
        Assert.Equal(0, freshness.Points);
        Assert.True(result.Score < 100);
    }

    [Fact]
    public void БезДатыПроверкиЗаСвежестьНеНаказываем()
    {
        var noDate = Clean();
        noDate.ScanFinishedAt = null;

        var result = SecurityCategory.Evaluate(With(noDate));

        Assert.Equal(100, result.Score);
    }

    // ---------- вес в общей оценке ----------

    [Fact]
    public void БезопасностьУчаствуетВОбщейОценке()
    {
        var facts = With(Clean());
        facts.Catalog!.Name = "org/project";

        var report = RepoHealthScore.Evaluate(facts);

        var security = report.Categories.Single(c => c.Name == SecurityCategory.Name);

        Assert.Equal(CategoryStatus.Scored, security.Status);
        Assert.True(security.AppliedWeight > 0);
    }

    [Fact]
    public void НаНаходкиДаютсяСоветы()
    {
        var security = Clean();
        security.Critical = 3;
        security.High = 6;

        var facts = With(security);
        facts.Catalog!.Name = "org/project";

        var report = RepoHealthScore.Evaluate(facts);

        var about = report.Recommendations
            .Where(advice => advice.Category == SecurityCategory.Name)
            .ToList();

        Assert.NotEmpty(about);
        Assert.All(about, advice =>
        {
            Assert.False(string.IsNullOrWhiteSpace(advice.Problem));
            Assert.False(string.IsNullOrWhiteSpace(advice.Action));
            Assert.True(advice.ExpectedGain > 0);
        });

        // Критические опаснее — совет про них должен стоять выше.
        Assert.Contains("критическ", about.First().Problem);
    }
}
