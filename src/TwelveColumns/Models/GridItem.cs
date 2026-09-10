namespace TwelveColumns;

/// <summary>
/// A single widget placed on the canvas. Coordinates are expressed in grid units:
/// <see cref="X"/>/<see cref="W"/> in columns, <see cref="Y"/>/<see cref="H"/> in rows.
/// </summary>
public class GridItem
{
    /// <summary>Stable identity of the widget. Used as the Blazor diffing key.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Optional caption rendered in the default widget header.</summary>
    public string? Title { get; set; }

    /// <summary>
    /// Free-form discriminator the host app uses to decide what to render
    /// (for example "chart", "table", "markdown").
    /// </summary>
    public string? Type { get; set; }

    /// <summary>Zero-based column of the widget's left edge.</summary>
    public int X { get; set; }

    /// <summary>Zero-based row of the widget's top edge.</summary>
    public int Y { get; set; }

    /// <summary>Width in columns.</summary>
    public int W { get; set; } = 3;

    /// <summary>Height in rows.</summary>
    public int H { get; set; } = 3;

    /// <summary>Smallest width the user may resize to.</summary>
    public int MinW { get; set; } = 1;

    /// <summary>Smallest height the user may resize to.</summary>
    public int MinH { get; set; } = 1;

    /// <summary>Largest width the user may resize to, or <c>null</c> for the column count.</summary>
    public int? MaxW { get; set; }

    /// <summary>Largest height the user may resize to, or <c>null</c> for unbounded.</summary>
    public int? MaxH { get; set; }

    /// <summary>When <c>false</c> the widget cannot be dragged, whatever the grid allows.</summary>
    public bool Draggable { get; set; } = true;

    /// <summary>When <c>false</c> the widget cannot be resized, whatever the grid allows.</summary>
    public bool Resizable { get; set; } = true;

    /// <summary>When <c>false</c> the default header hides its remove button.</summary>
    public bool Removable { get; set; } = true;

    /// <summary>
    /// A static widget never moves: it is not draggable, not resizable, and other
    /// widgets are pushed around it instead of through it.
    /// </summary>
    public bool Static { get; set; }

    /// <summary>Extra CSS classes appended to the widget element.</summary>
    public string? CssClass { get; set; }

    /// <summary>Arbitrary payload owned by the host app. Never serialized by the grid.</summary>
    public object? Data { get; set; }

    /// <summary>
    /// String settings persisted alongside the layout, for widget configuration
    /// that should survive a save/load round trip.
    /// </summary>
    public Dictionary<string, string> Settings { get; set; } = new();

    /// <summary>Effective maximum width given the grid's column count.</summary>
    internal int EffectiveMaxW(int columns) => Math.Min(MaxW ?? columns, columns);

    /// <summary>Effective maximum height.</summary>
    internal int EffectiveMaxH() => MaxH ?? int.MaxValue;

    /// <summary>Creates a detached copy, sharing the <see cref="Data"/> reference.</summary>
    public GridItem Clone() => new()
    {
        Id = Id,
        Title = Title,
        Type = Type,
        X = X,
        Y = Y,
        W = W,
        H = H,
        MinW = MinW,
        MinH = MinH,
        MaxW = MaxW,
        MaxH = MaxH,
        Draggable = Draggable,
        Resizable = Resizable,
        Removable = Removable,
        Static = Static,
        CssClass = CssClass,
        Data = Data,
        Settings = new Dictionary<string, string>(Settings),
    };

    /// <summary>True when the two rectangles overlap by at least one cell.</summary>
    public bool Overlaps(GridItem other) =>
        !ReferenceEquals(this, other) &&
        X < other.X + other.W && X + W > other.X &&
        Y < other.Y + other.H && Y + H > other.Y;

    public override string ToString() => $"{Id} ({X},{Y} {W}x{H})";
}
