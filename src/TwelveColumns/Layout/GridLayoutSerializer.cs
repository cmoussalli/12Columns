using System.Text.Json;
using System.Text.Json.Serialization;

namespace TwelveColumns;

/// <summary>
/// Converts a layout to and from JSON so dashboards can be persisted. Only layout data is
/// written — <see cref="GridItem.Data"/> belongs to the host app and is left alone.
/// </summary>
public static class GridLayoutSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class Dto
    {
        public string Id { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Type { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public int MinW { get; set; } = 1;
        public int MinH { get; set; } = 1;
        public int? MaxW { get; set; }
        public int? MaxH { get; set; }
        public bool Static { get; set; }
        public string? CssClass { get; set; }
        public Dictionary<string, string>? Settings { get; set; }
    }

    public static string Serialize(IEnumerable<GridItem> items) =>
        JsonSerializer.Serialize(items.Select(i => new Dto
        {
            Id = i.Id,
            Title = i.Title,
            Type = i.Type,
            X = i.X,
            Y = i.Y,
            W = i.W,
            H = i.H,
            MinW = i.MinW,
            MinH = i.MinH,
            MaxW = i.MaxW,
            MaxH = i.MaxH,
            Static = i.Static,
            CssClass = i.CssClass,
            Settings = i.Settings.Count > 0 ? i.Settings : null,
        }).ToList(), Options);

    public static List<GridItem> Deserialize(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<Dto>>(json, Options) ?? [];
        return dtos.Select(d => new GridItem
        {
            Id = string.IsNullOrEmpty(d.Id) ? Guid.NewGuid().ToString("N") : d.Id,
            Title = d.Title,
            Type = d.Type,
            X = d.X,
            Y = d.Y,
            W = Math.Max(1, d.W),
            H = Math.Max(1, d.H),
            MinW = Math.Max(1, d.MinW),
            MinH = Math.Max(1, d.MinH),
            MaxW = d.MaxW,
            MaxH = d.MaxH,
            Static = d.Static,
            CssClass = d.CssClass,
            Settings = d.Settings ?? new Dictionary<string, string>(),
        }).ToList();
    }
}
