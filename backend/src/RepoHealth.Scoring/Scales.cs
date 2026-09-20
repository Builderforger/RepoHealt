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
