using System.Text;

namespace PP2ProductionStats.Helpers;

public static class TextHelper
{
    public static string BuildText(
        Rational productionRate,
        Rational consumptionRate,
        CountDictionary<string> producers,
        CountDictionary<string> consumers)
    {
        var builder = new StringBuilder();
        builder.Append("<color=#00FF00>+</color> Max. production: ");
        builder.AppendLine($"{(decimal)productionRate.Numerator / productionRate.Denominator:F2}/min");

        foreach (var c in producers)
        {
            builder.AppendLine($"{c.Key}: {c.Value}");
        }

        builder.Append("<color=#FF0000>-</color> Max. consumption: ");
        builder.AppendLine($"{(decimal)consumptionRate.Numerator / consumptionRate.Denominator:F2}/min");

        foreach (var c in consumers)
        {
            builder.AppendLine($"{c.Key}: {c.Value}");
        }

        return builder.ToString();
    }
}