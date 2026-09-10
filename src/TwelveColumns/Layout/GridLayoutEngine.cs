namespace TwelveColumns;

/// <summary>
/// Pure layout maths for the canvas: collision detection, push-away resolution and
/// gravity compaction. Every method mutates the <see cref="GridItem"/> instances in place
/// and is completely independent of Blazor, so it is straightforward to unit test.
/// </summary>
public static class GridLayoutEngine
{
    private readonly record struct Rect(int X, int Y, int W, int H)
    {
        public bool Overlaps(GridItem o) =>
            X < o.X + o.W && X + W > o.X && Y < o.Y + o.H && Y + H > o.Y;
    }

    /// <summary>Reading order: top to bottom, then left to right.</summary>
    public static List<GridItem> Sort(IEnumerable<GridItem> items) =>
        items.OrderBy(i => i.Y).ThenBy(i => i.X).ToList();

    /// <summary>The first widget overlapping <paramref name="item"/>, or <c>null</c>.</summary>
    public static GridItem? FirstCollision(IEnumerable<GridItem> items, GridItem item) =>
        items.FirstOrDefault(item.Overlaps);

    /// <summary>Every widget overlapping <paramref name="item"/>.</summary>
    public static List<GridItem> AllCollisions(IEnumerable<GridItem> items, GridItem item) =>
        items.Where(item.Overlaps).ToList();

    private static GridItem? FirstCollision(IEnumerable<GridItem> items, Rect rect, GridItem ignore) =>
        items.FirstOrDefault(i => !ReferenceEquals(i, ignore) && rect.Overlaps(i));

    /// <summary>Forces a widget's size and position inside the grid's limits.</summary>
    public static void ClampToBounds(GridItem item, int columns)
    {
        var minW = Math.Clamp(item.MinW, 1, columns);
        item.W = Math.Clamp(item.W, minW, item.EffectiveMaxW(columns));
        item.H = Math.Clamp(item.H, Math.Max(1, item.MinH), Math.Max(1, item.EffectiveMaxH()));
        item.X = Math.Clamp(item.X, 0, Math.Max(0, columns - item.W));
        item.Y = Math.Max(0, item.Y);
    }

    /// <summary>Clamps every widget, then settles the layout.</summary>
    public static void Normalize(IList<GridItem> items, int columns, GridCompaction compaction, bool allowOverlap)
    {
        foreach (var item in items)
        {
            ClampToBounds(item, columns);
        }

        if (!allowOverlap)
        {
            Compact(items, columns, compaction);
        }
    }

    /// <summary>
    /// Applies gravity. With <see cref="GridCompaction.Vertical"/> every widget floats up
    /// until it lands on another widget or the top edge; with <see cref="GridCompaction.None"/>
    /// widgets keep their row and are only nudged down far enough to stop overlapping.
    /// Static widgets never move and are reserved first.
    /// </summary>
    public static void Compact(IList<GridItem> items, int columns, GridCompaction compaction)
    {
        // Static widgets are obstacles, so they claim their cells before anything else.
        var placed = items.Where(i => i.Static).ToList();

        foreach (var item in Sort(items))
        {
            if (item.Static)
            {
                continue;
            }

            item.X = Math.Clamp(item.X, 0, Math.Max(0, columns - item.W));

            if (compaction == GridCompaction.Vertical)
            {
                item.Y = 0;
            }

            while (FirstCollision(placed, item) is not null)
            {
                item.Y++;
            }

            placed.Add(item);
        }
    }

    /// <summary>
    /// Moves a widget to (<paramref name="x"/>, <paramref name="y"/>), shuffling whatever it
    /// runs into out of the way, then re-settles the layout.
    /// </summary>
    /// <returns><c>true</c> if the layout actually changed.</returns>
    public static bool TryMove(
        IList<GridItem> items,
        GridItem item,
        int x,
        int y,
        int columns,
        GridCompaction compaction,
        bool preventCollision,
        bool allowOverlap)
    {
        if (item.Static)
        {
            return false;
        }

        var targetX = Math.Clamp(x, 0, Math.Max(0, columns - item.W));
        var targetY = Math.Max(0, y);
        if (targetX == item.X && targetY == item.Y)
        {
            return false;
        }

        if (allowOverlap)
        {
            item.X = targetX;
            item.Y = targetY;
            return true;
        }

        if (preventCollision)
        {
            var probe = new Rect(targetX, targetY, item.W, item.H);
            if (FirstCollision(items, probe, item) is not null)
            {
                return false;
            }
        }

        var moved = NewMovedSet();
        MoveElement(items, item, targetX, targetY, isUserAction: true, columns, compaction, moved);
        Compact(items, columns, compaction);
        return true;
    }

    /// <summary>
    /// Resizes a widget, honouring its min/max limits and the column count, then pushes
    /// anything it grew into out of the way. <paramref name="x"/>/<paramref name="y"/> allow
    /// resizing from a top or left grip, which moves the origin as well.
    /// </summary>
    /// <returns><c>true</c> if the layout actually changed.</returns>
    public static bool TryResize(
        IList<GridItem> items,
        GridItem item,
        int x,
        int y,
        int w,
        int h,
        int columns,
        GridCompaction compaction,
        bool preventCollision,
        bool allowOverlap)
    {
        if (item.Static)
        {
            return false;
        }

        var minW = Math.Clamp(item.MinW, 1, columns);
        var newW = Math.Clamp(w, minW, item.EffectiveMaxW(columns));
        var newH = Math.Clamp(h, Math.Max(1, item.MinH), Math.Max(1, item.EffectiveMaxH()));

        // A grip on the north or west edge moves the origin; clamping the size above may have
        // eaten part of the requested travel, so re-derive the origin from the far edge.
        var newX = x;
        var newY = y;
        if (x != item.X)
        {
            newX = item.X + item.W - newW;
        }

        if (y != item.Y)
        {
            newY = item.Y + item.H - newH;
        }

        newX = Math.Clamp(newX, 0, Math.Max(0, columns - newW));
        newY = Math.Max(0, newY);

        if (newX == item.X && newY == item.Y && newW == item.W && newH == item.H)
        {
            return false;
        }

        var previous = (item.X, item.Y, item.W, item.H);
        item.X = newX;
        item.Y = newY;
        item.W = newW;
        item.H = newH;

        if (allowOverlap)
        {
            return true;
        }

        if (preventCollision && FirstCollision(items, item) is not null)
        {
            (item.X, item.Y, item.W, item.H) = previous;
            return false;
        }

        // Growing into a neighbour should push it away rather than swap with it, so this
        // deliberately does not use the "try above first" user-action heuristic.
        var moved = NewMovedSet();
        moved.Add(item);
        foreach (var collision in Sort(AllCollisions(items, item)))
        {
            if (moved.Contains(collision) || collision.Static)
            {
                continue;
            }

            MoveElementAwayFromCollision(items, item, collision, isUserAction: false, columns, compaction, moved);
        }

        Compact(items, columns, compaction);
        return true;
    }

    /// <summary>
    /// Finds the topmost, leftmost free rectangle of <paramref name="w"/>x<paramref name="h"/> cells.
    /// Always succeeds: the canvas grows downwards.
    /// </summary>
    public static (int X, int Y) FindFreeSpot(IEnumerable<GridItem> items, int w, int h, int columns)
    {
        var list = items as IReadOnlyCollection<GridItem> ?? items.ToList();
        var width = Math.Clamp(w, 1, Math.Max(1, columns));
        var height = Math.Max(1, h);
        var maxY = list.Count == 0 ? 0 : list.Max(i => i.Y + i.H);

        for (var y = 0; y <= maxY; y++)
        {
            for (var x = 0; x + width <= columns; x++)
            {
                var candidate = new Rect(x, y, width, height);
                if (!list.Any(candidate.Overlaps))
                {
                    return (x, y);
                }
            }
        }

        return (0, maxY);
    }

    /// <summary>Number of rows the layout occupies.</summary>
    public static int RowCount(IEnumerable<GridItem> items) =>
        items.Select(i => i.Y + i.H).DefaultIfEmpty(0).Max();

    // GridItem does not override Equals, so the default set already compares by reference.
    private static HashSet<GridItem> NewMovedSet() => new();

    private static void MoveElement(
        IList<GridItem> items,
        GridItem item,
        int? x,
        int? y,
        bool isUserAction,
        int columns,
        GridCompaction compaction,
        HashSet<GridItem> moved)
    {
        if (item.Static)
        {
            return;
        }

        var oldY = item.Y;
        if (x is int nx)
        {
            item.X = Math.Clamp(nx, 0, Math.Max(0, columns - item.W));
        }

        if (y is int ny)
        {
            item.Y = Math.Max(0, ny);
        }

        moved.Add(item);

        // When a widget travels upwards its collisions must be resolved bottom-up, otherwise
        // the widget it displaces gets pushed onto the one below it in the same pass.
        var ordered = Sort(items);
        var movingUp = compaction == GridCompaction.Vertical && y is not null && oldY >= item.Y;
        if (movingUp)
        {
            ordered.Reverse();
        }

        foreach (var collision in AllCollisions(ordered, item))
        {
            if (moved.Contains(collision))
            {
                continue;
            }

            if (collision.Static)
            {
                // Can't shove a static widget, so the mover is the one that yields.
                MoveElementAwayFromCollision(items, collision, item, isUserAction, columns, compaction, moved);
            }
            else
            {
                MoveElementAwayFromCollision(items, item, collision, isUserAction, columns, compaction, moved);
            }
        }
    }

    private static void MoveElementAwayFromCollision(
        IList<GridItem> items,
        GridItem collidesWith,
        GridItem itemToMove,
        bool isUserAction,
        int columns,
        GridCompaction compaction,
        HashSet<GridItem> moved)
    {
        if (isUserAction)
        {
            // The user dragged something onto this widget: prefer swapping it to the space
            // just above, which reads as "the two panels traded places" rather than a shove.
            var above = new Rect(itemToMove.X, Math.Max(collidesWith.Y - itemToMove.H, 0), itemToMove.W, itemToMove.H);
            var blocker = FirstCollision(items, above, itemToMove);

            if (blocker is null)
            {
                MoveElement(items, itemToMove, null, above.Y, isUserAction: false, columns, compaction, moved);
                return;
            }

            if (blocker.Y + blocker.H > collidesWith.Y)
            {
                // No room above, so drop it immediately below and let compaction pull it back.
                MoveElement(items, itemToMove, null, collidesWith.Y + 1, isUserAction: false, columns, compaction, moved);
                return;
            }
        }

        MoveElement(items, itemToMove, null, itemToMove.Y + 1, isUserAction: false, columns, compaction, moved);
    }
}
