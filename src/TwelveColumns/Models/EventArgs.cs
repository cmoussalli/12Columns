namespace TwelveColumns;

/// <summary>Raised whenever the grid rewrites widget positions or sizes.</summary>
public sealed class GridLayoutChangedEventArgs : EventArgs
{
    public GridLayoutChangedEventArgs(GridChangeReason reason, IReadOnlyList<GridItem> items, GridItem? item)
    {
        Reason = reason;
        Items = items;
        Item = item;
    }

    /// <summary>What triggered the change.</summary>
    public GridChangeReason Reason { get; }

    /// <summary>The live layout after the change.</summary>
    public IReadOnlyList<GridItem> Items { get; }

    /// <summary>The widget the user acted on, when the change came from a single widget.</summary>
    public GridItem? Item { get; }
}

/// <summary>Raised at the start and end of a drag or resize gesture.</summary>
public sealed class GridInteractionEventArgs : EventArgs
{
    public GridInteractionEventArgs(GridInteraction interaction, GridItem item)
    {
        Interaction = interaction;
        Item = item;
    }

    public GridInteraction Interaction { get; }
    public GridItem Item { get; }
}

/// <summary>
/// Raised when something is dropped onto the canvas from outside the grid — typically a
/// widget palette. Set <see cref="Item"/> to have the grid insert a widget at the drop point.
/// </summary>
public sealed class GridExternalDropEventArgs : EventArgs
{
    public GridExternalDropEventArgs(string payload, int x, int y)
    {
        Payload = payload;
        X = x;
        Y = y;
    }

    /// <summary>The <c>text/plain</c> value carried by the drag source.</summary>
    public string Payload { get; }

    /// <summary>Column the pointer was over.</summary>
    public int X { get; }

    /// <summary>Row the pointer was over.</summary>
    public int Y { get; }

    /// <summary>
    /// Assign a widget here to add it at the drop point. Its <see cref="GridItem.X"/> and
    /// <see cref="GridItem.Y"/> are overwritten with <see cref="X"/>/<see cref="Y"/>.
    /// Leave <c>null</c> to ignore the drop.
    /// </summary>
    public GridItem? Item { get; set; }
}
