using System.Globalization;

namespace TwelveColumns.Demo.Components.Widgets;

/// <summary>
/// Deterministic sample numbers. Everything is derived from the widget id so a panel keeps
/// the same shape across the many re-renders a drag causes.
/// </summary>
public static class DemoData
{
    private static readonly string[] Regions = ["EMEA", "APAC", "LATAM", "North America", "Nordics"];
    private static readonly string[] Metrics = ["Sessions", "Signups", "Revenue", "Errors", "Latency"];

    public static int Seed(string id)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in id)
            {
                hash = (hash * 31) + c;
            }

            return hash & 0x7fffffff;
        }
    }

    public static double[] Series(string id, int count, double min = 20, double max = 100)
    {
        var rng = new Random(Seed(id));
        var values = new double[count];
        var value = min + (rng.NextDouble() * (max - min));

        for (var i = 0; i < count; i++)
        {
            // A random walk reads as real data far better than independent samples.
            value += (rng.NextDouble() - 0.45) * (max - min) * 0.28;
            values[i] = Math.Clamp(value, min, max);
        }

        return values;
    }

    public static string Metric(string id) => Metrics[Seed(id) % Metrics.Length];

    public static string Region(string id, int index) => Regions[(Seed(id) + index) % Regions.Length];

    public static string Format(double value) =>
        value >= 1000
            ? (value / 1000).ToString("0.0", CultureInfo.InvariantCulture) + "k"
            : value.ToString("0.#", CultureInfo.InvariantCulture);
}
