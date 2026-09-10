using TwelveColumns;

namespace TwelveColumns.Tests;

public class GridLayoutEngineTests
{
    private const int Columns = 12;

    private static GridItem Item(string id, int x, int y, int w, int h) =>
        new() { Id = id, X = x, Y = y, W = w, H = h };

    private static (int X, int Y, int W, int H) At(IEnumerable<GridItem> items, string id)
    {
        var item = items.Single(i => i.Id == id);
        return (item.X, item.Y, item.W, item.H);
    }

    private static void AssertNoOverlaps(IReadOnlyList<GridItem> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            for (var j = i + 1; j < items.Count; j++)
            {
                Assert.False(
                    items[i].Overlaps(items[j]),
                    $"{items[i]} overlaps {items[j]}");
            }
        }
    }

    // ---------------------------------------------------------------- collisions

    [Theory]
    [InlineData(0, 0, 2, 2, true)]   // exact same cell
    [InlineData(1, 1, 2, 2, true)]   // corner overlap
    [InlineData(2, 0, 2, 2, false)]  // touching on the right edge
    [InlineData(0, 2, 2, 2, false)]  // touching on the bottom edge
    [InlineData(3, 3, 2, 2, false)]  // far away
    public void Overlaps_detects_only_shared_cells(int x, int y, int w, int h, bool expected)
    {
        var a = Item("a", 0, 0, 2, 2);
        var b = Item("b", x, y, w, h);

        Assert.Equal(expected, a.Overlaps(b));
        Assert.Equal(expected, b.Overlaps(a));
    }

    [Fact]
    public void Overlaps_is_false_for_the_same_instance()
    {
        var a = Item("a", 0, 0, 2, 2);
        Assert.False(a.Overlaps(a));
    }

    // ---------------------------------------------------------------- compaction

    [Fact]
    public void Vertical_compaction_pulls_widgets_to_the_top()
    {
        List<GridItem> items = [Item("a", 0, 5, 3, 2), Item("b", 3, 9, 3, 2)];

        GridLayoutEngine.Compact(items, Columns, GridCompaction.Vertical);

        Assert.Equal((0, 0, 3, 2), At(items, "a"));
        Assert.Equal((3, 0, 3, 2), At(items, "b"));
    }

    [Fact]
    public void Vertical_compaction_stacks_widgets_that_share_columns()
    {
        List<GridItem> items = [Item("a", 0, 4, 3, 2), Item("b", 0, 9, 3, 3)];

        GridLayoutEngine.Compact(items, Columns, GridCompaction.Vertical);

        Assert.Equal((0, 0, 3, 2), At(items, "a"));
        Assert.Equal((0, 2, 3, 3), At(items, "b"));
    }

    [Fact]
    public void No_compaction_keeps_rows_but_still_separates_overlaps()
    {
        List<GridItem> items = [Item("a", 0, 5, 3, 2), Item("b", 0, 5, 3, 2)];

        GridLayoutEngine.Compact(items, Columns, GridCompaction.None);

        Assert.Equal(5, items.Single(i => i.Id == "a").Y);
        AssertNoOverlaps(items);
    }

    [Fact]
    public void Static_widgets_are_never_moved_by_compaction()
    {
        var pinned = Item("pinned", 0, 6, 4, 2);
        pinned.Static = true;
        List<GridItem> items = [pinned, Item("a", 0, 0, 4, 3)];

        GridLayoutEngine.Compact(items, Columns, GridCompaction.Vertical);

        Assert.Equal((0, 6, 4, 2), At(items, "pinned"));
        Assert.Equal((0, 0, 4, 3), At(items, "a"));
        AssertNoOverlaps(items);
    }

    [Fact]
    public void Compaction_routes_widgets_around_a_static_widget()
    {
        var pinned = Item("pinned", 0, 2, 4, 2);
        pinned.Static = true;
        // "a" would compact to y=0..3 and run straight through the pinned widget.
        List<GridItem> items = [pinned, Item("a", 0, 8, 4, 3)];

        GridLayoutEngine.Compact(items, Columns, GridCompaction.Vertical);

        Assert.Equal((0, 2, 4, 2), At(items, "pinned"));
        Assert.Equal(4, items.Single(i => i.Id == "a").Y);
        AssertNoOverlaps(items);
    }

    // ---------------------------------------------------------------- moving

    [Fact]
    public void Dragging_a_widget_down_onto_another_swaps_them()
    {
        var a = Item("a", 0, 0, 6, 3);
        List<GridItem> items = [a, Item("b", 0, 3, 6, 3)];

        GridLayoutEngine.TryMove(items, a, 0, 3, Columns, GridCompaction.Vertical, false, false);

        Assert.Equal((0, 0, 6, 3), At(items, "b"));
        Assert.Equal((0, 3, 6, 3), At(items, "a"));
        AssertNoOverlaps(items);
    }

    [Fact]
    public void Dragging_a_widget_up_onto_another_swaps_them()
    {
        var b = Item("b", 0, 3, 6, 3);
        List<GridItem> items = [Item("a", 0, 0, 6, 3), b];

        GridLayoutEngine.TryMove(items, b, 0, 0, Columns, GridCompaction.Vertical, false, false);

        Assert.Equal((0, 0, 6, 3), At(items, "b"));
        Assert.Equal((0, 3, 6, 3), At(items, "a"));
        AssertNoOverlaps(items);
    }

    [Fact]
    public void Moving_sideways_into_free_space_leaves_neighbours_alone()
    {
        var a = Item("a", 0, 0, 3, 3);
        List<GridItem> items = [a, Item("b", 6, 0, 3, 3)];

        GridLayoutEngine.TryMove(items, a, 3, 0, Columns, GridCompaction.Vertical, false, false);

        Assert.Equal((3, 0, 3, 3), At(items, "a"));
        Assert.Equal((6, 0, 3, 3), At(items, "b"));
    }

    [Fact]
    public void Moves_are_clamped_to_the_column_count()
    {
        var a = Item("a", 0, 0, 4, 2);
        List<GridItem> items = [a];

        GridLayoutEngine.TryMove(items, a, 20, -5, Columns, GridCompaction.Vertical, false, false);

        Assert.Equal((8, 0, 4, 2), At(items, "a"));
    }

    [Fact]
    public void PreventCollision_rejects_a_move_onto_an_occupied_cell()
    {
        var a = Item("a", 0, 0, 3, 3);
        List<GridItem> items = [a, Item("b", 3, 0, 3, 3)];

        var moved = GridLayoutEngine.TryMove(items, a, 3, 0, Columns, GridCompaction.Vertical, preventCollision: true, allowOverlap: false);

        Assert.False(moved);
        Assert.Equal((0, 0, 3, 3), At(items, "a"));
        Assert.Equal((3, 0, 3, 3), At(items, "b"));
    }

    [Fact]
    public void AllowOverlap_lets_widgets_sit_on_top_of_each_other()
    {
        var a = Item("a", 0, 0, 3, 3);
        List<GridItem> items = [a, Item("b", 3, 0, 3, 3)];

        var moved = GridLayoutEngine.TryMove(items, a, 3, 0, Columns, GridCompaction.Vertical, preventCollision: false, allowOverlap: true);

        Assert.True(moved);
        Assert.Equal((3, 0, 3, 3), At(items, "a"));
        Assert.Equal((3, 0, 3, 3), At(items, "b"));
    }

    [Fact]
    public void A_static_widget_cannot_be_moved()
    {
        var pinned = Item("pinned", 0, 0, 3, 3);
        pinned.Static = true;
        List<GridItem> items = [pinned];

        Assert.False(GridLayoutEngine.TryMove(items, pinned, 5, 5, Columns, GridCompaction.Vertical, false, false));
        Assert.Equal((0, 0, 3, 3), At(items, "pinned"));
    }

    [Fact]
    public void Dragging_onto_a_static_widget_leaves_it_in_place()
    {
        var pinned = Item("pinned", 4, 0, 4, 3);
        pinned.Static = true;
        var a = Item("a", 0, 0, 4, 3);
        List<GridItem> items = [pinned, a];

        GridLayoutEngine.TryMove(items, a, 4, 0, Columns, GridCompaction.Vertical, false, false);

        Assert.Equal((4, 0, 4, 3), At(items, "pinned"));
        AssertNoOverlaps(items);
    }

    // ---------------------------------------------------------------- resizing

    [Fact]
    public void Growing_a_widget_pushes_the_one_below_down()
    {
        var a = Item("a", 0, 0, 6, 2);
        List<GridItem> items = [a, Item("b", 0, 2, 6, 2)];

        GridLayoutEngine.TryResize(items, a, 0, 0, 6, 4, Columns, GridCompaction.Vertical, false, false);

        Assert.Equal((0, 0, 6, 4), At(items, "a"));
        Assert.Equal((0, 4, 6, 2), At(items, "b"));
    }

    [Fact]
    public void Resize_respects_min_and_max_limits()
    {
        var a = new GridItem { Id = "a", X = 0, Y = 0, W = 4, H = 4, MinW = 3, MinH = 2, MaxW = 6, MaxH = 5 };
        List<GridItem> items = [a];

        GridLayoutEngine.TryResize(items, a, 0, 0, 1, 1, Columns, GridCompaction.Vertical, false, false);
        Assert.Equal((0, 0, 3, 2), At(items, "a"));

        GridLayoutEngine.TryResize(items, a, 0, 0, 99, 99, Columns, GridCompaction.Vertical, false, false);
        Assert.Equal((0, 0, 6, 5), At(items, "a"));
    }

    [Fact]
    public void Resize_is_clamped_to_the_column_count()
    {
        var a = Item("a", 8, 0, 4, 2);
        List<GridItem> items = [a];

        GridLayoutEngine.TryResize(items, a, 8, 0, 12, 2, Columns, GridCompaction.Vertical, false, false);

        Assert.Equal(12, items[0].X + items[0].W);
        Assert.True(items[0].W <= Columns);
    }

    [Fact]
    public void Resizing_from_the_west_grip_moves_the_origin_and_keeps_the_right_edge()
    {
        var a = Item("a", 4, 0, 4, 2);
        List<GridItem> items = [a];

        // Dragging the west grip two columns left: x 4→2, w 4→6, right edge stays at 8.
        GridLayoutEngine.TryResize(items, a, 2, 0, 6, 2, Columns, GridCompaction.Vertical, false, false);

        Assert.Equal((2, 0, 6, 2), At(items, "a"));
    }

    [Fact]
    public void Resizing_from_the_west_grip_stops_at_the_minimum_width()
    {
        var a = new GridItem { Id = "a", X = 2, Y = 0, W = 6, H = 2, MinW = 4 };
        List<GridItem> items = [a];

        // Asking for w=1 clamps to 4, and the origin is re-derived so the right edge holds at 8.
        GridLayoutEngine.TryResize(items, a, 7, 0, 1, 2, Columns, GridCompaction.Vertical, false, false);

        Assert.Equal((4, 0, 4, 2), At(items, "a"));
    }

    // ---------------------------------------------------------------- placement

    [Fact]
    public void FindFreeSpot_returns_the_topmost_leftmost_gap()
    {
        List<GridItem> items = [Item("a", 0, 0, 6, 2)];

        Assert.Equal((6, 0), GridLayoutEngine.FindFreeSpot(items, 6, 2, Columns));
    }

    [Fact]
    public void FindFreeSpot_falls_through_to_a_new_row_when_nothing_fits()
    {
        List<GridItem> items = [Item("a", 0, 0, 12, 3)];

        Assert.Equal((0, 3), GridLayoutEngine.FindFreeSpot(items, 12, 2, Columns));
    }

    [Fact]
    public void FindFreeSpot_on_an_empty_canvas_is_the_origin()
    {
        Assert.Equal((0, 0), GridLayoutEngine.FindFreeSpot([], 4, 2, Columns));
    }

    [Fact]
    public void RowCount_measures_the_bottom_edge()
    {
        List<GridItem> items = [Item("a", 0, 0, 3, 2), Item("b", 3, 4, 3, 3)];

        Assert.Equal(7, GridLayoutEngine.RowCount(items));
        Assert.Equal(0, GridLayoutEngine.RowCount([]));
    }

    // ---------------------------------------------------------------- normalization

    [Fact]
    public void Normalize_shrinks_widgets_that_are_wider_than_the_grid()
    {
        List<GridItem> items = [Item("a", 0, 0, 12, 2)];

        GridLayoutEngine.Normalize(items, columns: 6, GridCompaction.Vertical, allowOverlap: false);

        Assert.Equal((0, 0, 6, 2), At(items, "a"));
    }

    [Fact]
    public void Normalize_pulls_widgets_back_inside_a_narrowed_grid()
    {
        List<GridItem> items = [Item("a", 9, 0, 3, 2), Item("b", 0, 0, 3, 2)];

        GridLayoutEngine.Normalize(items, columns: 6, GridCompaction.Vertical, allowOverlap: false);

        Assert.All(items, i => Assert.True(i.X + i.W <= 6, $"{i} escapes a 6-column grid"));
        AssertNoOverlaps(items);
    }

    [Fact]
    public void Normalize_leaves_overlaps_alone_when_overlap_is_allowed()
    {
        List<GridItem> items = [Item("a", 0, 3, 3, 2), Item("b", 0, 3, 3, 2)];

        GridLayoutEngine.Normalize(items, Columns, GridCompaction.Vertical, allowOverlap: true);

        Assert.Equal((0, 3, 3, 2), At(items, "a"));
        Assert.Equal((0, 3, 3, 2), At(items, "b"));
    }

    // ---------------------------------------------------------------- invariants

    [Fact]
    public void A_long_run_of_random_drags_never_produces_an_overlap()
    {
        var rng = new Random(20260908);
        List<GridItem> items =
        [
            Item("a", 0, 0, 3, 2), Item("b", 3, 0, 6, 4), Item("c", 9, 0, 3, 3),
            Item("d", 0, 2, 3, 3), Item("e", 3, 4, 4, 2), Item("f", 7, 4, 5, 3),
        ];

        for (var step = 0; step < 500; step++)
        {
            var item = items[rng.Next(items.Count)];
            GridLayoutEngine.TryMove(
                items, item, rng.Next(0, Columns), rng.Next(0, 12),
                Columns, GridCompaction.Vertical, preventCollision: false, allowOverlap: false);

            AssertNoOverlaps(items);
            Assert.All(items, i => Assert.True(i.X >= 0 && i.X + i.W <= Columns, $"{i} escaped the grid"));
            Assert.All(items, i => Assert.True(i.Y >= 0, $"{i} escaped upwards"));
        }
    }

    [Fact]
    public void A_long_run_of_random_resizes_never_produces_an_overlap()
    {
        var rng = new Random(8092026);
        List<GridItem> items =
        [
            Item("a", 0, 0, 4, 2), Item("b", 4, 0, 4, 3), Item("c", 8, 0, 4, 2),
            Item("d", 0, 3, 6, 3), Item("e", 6, 3, 6, 2),
        ];

        for (var step = 0; step < 500; step++)
        {
            var item = items[rng.Next(items.Count)];
            GridLayoutEngine.TryResize(
                items, item, item.X, item.Y, rng.Next(1, Columns + 1), rng.Next(1, 8),
                Columns, GridCompaction.Vertical, preventCollision: false, allowOverlap: false);

            AssertNoOverlaps(items);
            Assert.All(items, i => Assert.True(i.X >= 0 && i.X + i.W <= Columns, $"{i} escaped the grid"));
        }
    }
}
