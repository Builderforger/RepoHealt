using System;
using System.Collections.Generic;
using System.Text;

namespace RepoHealth.Scoring;

public class Scales
{
    public static double Grows(double value, double fullAt)
    {
        if (value <= 0 || fullAt <= 0)
            return 0;

        return Math.Min(value / fullAt, 1.0);
    }
    /// Чем больше находок, тем ниже балл. Это та же убывающая
    /// шкала, что и Fades, только считает не дни, а количество:
    /// при fine и меньше — полный балл, при tooMany и больше — ноль.
    public static double? Fewer(int? count, int fine, int tooMany)
    {
        return Fades(count, fine, tooMany);
    }

    public static double? Fades(int? days, int good, int bad)
    {
        if (good >= bad)
            throw new ArgumentException("Порог good должен быть меньше bad.");

        if (days is null)
            return null;
        
        if (days < good)
            return 1.0;

        if (days > bad)
            return 0.0;
         
        return 1.0 - ((double)(days - good) / (bad - good));
    }
}
