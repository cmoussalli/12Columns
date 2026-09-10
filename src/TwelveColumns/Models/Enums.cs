namespace TwelveColumns;

/// <summary>How the grid closes vertical gaps after a widget moves.</summary>
public enum GridCompaction
{
    /// <summary>Widgets float up until they hit another widget or the top edge.</summary>
    Vertical,

    /// <summary>Widgets stay exactly where they are dropped (free-form canvas).</summary>
    None,
}

/// <summary>Which resize grips are rendered on a widget.</summary>
[Flags]
public enum ResizeHandles
{
    None = 0,
    North = 1 << 0,
    East = 1 << 1,
    South = 1 << 2,
    West = 1 << 3,
    NorthEast = 1 << 4,
    SouthEast = 1 << 5,
    SouthWest = 1 << 6,
    NorthWest = 1 << 7,

    /// <summary>The default: right edge, bottom edge and bottom-right corner.</summary>
    Default = East | South | SouthEast,

    Corners = NorthEast | SouthEast | SouthWest | NorthWest,
    Edges = North | East | South | West,
    All = Edges | Corners,
}

/// <summary>What the user is currently doing to a widget.</summary>
public enum GridInteraction
{
    None,
    Drag,
    Resize,
}

/// <summary>Why the layout changed.</summary>
public enum GridChangeReason
{
    Drag,
    Resize,
    Add,
    Remove,
    Compact,
    ColumnsChanged,
    Programmatic,
}
