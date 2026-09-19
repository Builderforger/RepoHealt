namespace RepoHealth.Scoring.Tests;

/// <summary>
/// Проверяет две шкалы, на которых держится весь расчёт.
/// </summary>
public class ScalesTests
{
    // ---------- Grows: чем больше, тем лучше ----------

    // [Theory] — один тест на несколько наборов чисел.
    // Каждая строчка [InlineData] запускается отдельно,
    // и в отчёте видно, какая именно не прошла.
    [Theory]
    [InlineData(0, 120, 0.0)]      // ничего нет
    [InlineData(30, 120, 0.25)]    // четверть пути
    [InlineData(60, 120, 0.5)]     // половина
    [InlineData(120, 120, 1.0)]    // дошли до потолка
    [InlineData(300, 120, 1.0)]    // выше потолка не поднимаемся
    public void GrowsToTheCeiling(double value, double fullAt, double expected)
    {
        // тройка в конце — сверять до трёх знаков после запятой
        Assert.Equal(expected, Scales.Grows(value, fullAt), 3);
    }

    [Fact]
    public void GrowsDoesNotGoBelowZero()
    {
        Assert.Equal(0.0, Scales.Grows(-5, 120), 3);
    }

    [Fact]
    public void GrowsHandlesZeroCeiling()
    {
        // деления на ноль быть не должно
        Assert.Equal(0.0, Scales.Grows(50, 0), 3);
    }

    // ---------- Fades: чем свежее, тем лучше ----------

    [Theory]
    [InlineData(3, 1.0)]      // совсем свежий
    [InlineData(14, 1.0)]     // ровно на границе «хорошо»
    [InlineData(97, 0.5)]     // посередине между 14 и 180
    [InlineData(180, 0.0)]    // дошли до «плохо»
    [InlineData(400, 0.0)]    // давно мёртв
    public void FadesDecreasesWithTime(int days, double expected)
    {
        var actual = Scales.Fades(days, good: 14, bad: 180);

        Assert.NotNull(actual);
        Assert.Equal(expected, actual!.Value, 2);
    }

    [Fact]
    public void FadesReturnsNullForMissingData()
    {
        // главное свойство: отсутствие данных не превращается
        // ни в ноль, ни в единицу — оно остаётся неизвестностью
        Assert.Null(Scales.Fades(null, good: 14, bad: 180));
    }

    [Fact]
    public void FadesThrowsArgumentExceptionForReversedThresholds()
    {
        // good и bad поменяли местами — это ошибка вызывающего,
        // и она должна вскрываться сразу, а не тихо портить оценку
        Assert.Throws<ArgumentException>(
            () => Scales.Fades(3, good: 180, bad: 14));
    }
}
