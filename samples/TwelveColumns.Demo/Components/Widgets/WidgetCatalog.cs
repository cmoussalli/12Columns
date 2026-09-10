using TwelveColumns;

namespace TwelveColumns.Demo.Components.Widgets;

/// <summary>A kind of widget the demo can put on the canvas.</summary>
/// <param name="Key">Matches <see cref="GridItem.Type"/>.</param>
public sealed record WidgetKind(string Key, string Label, int W, int H, int MinW, int MinH);

/// <summary>
/// The demo's widget palette. A real app would build this from whatever panel types it
/// supports; the canvas itself has no opinion about widget content.
/// </summary>
public static class WidgetCatalog
{
    public static readonly IReadOnlyList<WidgetKind> All =
    [
        new("stat", "Stat", 3, 2, 2, 2),
        new("line", "Line chart", 6, 4, 3, 3),
        new("bars", "Bar chart", 4, 4, 2, 3),
        new("donut", "Donut", 3, 4, 2, 3),
        new("table", "Table", 5, 4, 3, 3),
        new("clock", "Live clock", 3, 2, 2, 2),
        new("text", "Notes", 4, 3, 2, 2),
    ];

    public static WidgetKind Find(string key) =>
        All.FirstOrDefault(k => k.Key == key) ?? All[0];

    private static int _counter;

    /// <summary>Builds a widget of the given kind, sized to that kind's defaults.</summary>
    public static GridItem Create(string key)
    {
        var kind = Find(key);
        var number = Interlocked.Increment(ref _counter);

        return new GridItem
        {
            Id = $"{kind.Key}-{number}",
            Type = kind.Key,
            Title = $"{kind.Label} {number}",
            W = kind.W,
            H = kind.H,
            MinW = kind.MinW,
            MinH = kind.MinH,
        };
    }
}
