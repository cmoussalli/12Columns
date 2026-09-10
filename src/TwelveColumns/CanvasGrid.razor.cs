using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace TwelveColumns;

/// <summary>
/// A 12-column (configurable) dashboard canvas. Widgets can be dragged, resized, added and
/// removed by the end user, and the whole layout is plain <see cref="GridItem"/> data the
/// host app owns and can persist.
/// </summary>
public partial class CanvasGrid : ComponentBase, IAsyncDisposable
{
    private const string ModulePath = "./_content/TwelveColumns/twelvecolumns.js";

    private ElementReference _element;
    private IJSObjectReference? _module;
    private DotNetObjectReference<CanvasGrid>? _selfRef;
    private readonly List<GridItem> _fallbackItems = [];
    private IList<GridItem> _items;
    private GridItem? _activeItem;
    private GridInteraction _interaction = GridInteraction.None;
    private bool _jsInitialized;
    private int _lastColumns;
    private string? _lastJsOptions;

    public CanvasGrid() => _items = _fallbackItems;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The widgets on the canvas. Mutated in place as the user interacts.</summary>
    [Parameter] public IList<GridItem>? Items { get; set; }

    /// <summary>Fires whenever the grid changes <see cref="Items"/>.</summary>
    [Parameter] public EventCallback<IList<GridItem>> ItemsChanged { get; set; }

    /// <summary>Number of columns the canvas is divided into. Defaults to 12.</summary>
    [Parameter] public int Columns { get; set; } = 12;

    /// <summary>Height of a single row, in pixels.</summary>
    [Parameter] public int RowHeight { get; set; } = 60;

    /// <summary>Space between cells, in pixels. Applied both horizontally and vertically.</summary>
    [Parameter] public int Gap { get; set; } = 10;

    /// <summary>Minimum number of rows the canvas reserves even when it is empty.</summary>
    [Parameter] public int MinRows { get; set; } = 4;

    /// <summary>
    /// Master switch for authoring. When <c>false</c> the canvas is read-only: no drag,
    /// no resize, no remove buttons, no grid lines.
    /// </summary>
    [Parameter] public bool EditMode { get; set; } = true;

    /// <summary>Allows dragging. Combined with <see cref="GridItem.Draggable"/> per widget.</summary>
    [Parameter] public bool Draggable { get; set; } = true;

    /// <summary>Allows resizing. Combined with <see cref="GridItem.Resizable"/> per widget.</summary>
    [Parameter] public bool Resizable { get; set; } = true;

    /// <summary>Shows the remove button. Combined with <see cref="GridItem.Removable"/> per widget.</summary>
    [Parameter] public bool Removable { get; set; } = true;

    /// <summary>Which resize grips to render. Defaults to right edge, bottom edge and corner.</summary>
    [Parameter] public ResizeHandles ResizeHandles { get; set; } = ResizeHandles.Default;

    /// <summary>
    /// When <c>true</c> the whole widget starts a drag; otherwise only its header does.
    /// Keep the default when widgets contain interactive content.
    /// </summary>
    [Parameter] public bool DragFromAnywhere { get; set; }

    /// <summary>Renders the built-in widget header with the title and remove button.</summary>
    [Parameter] public bool ShowHeader { get; set; } = true;

    /// <summary>Draws column guides and row lines while in edit mode.</summary>
    [Parameter] public bool ShowGridLines { get; set; } = true;

    /// <summary>How the canvas closes vertical gaps.</summary>
    [Parameter] public GridCompaction Compaction { get; set; } = GridCompaction.Vertical;

    /// <summary>Refuses a move or resize that would overlap another widget instead of shuffling.</summary>
    [Parameter] public bool PreventCollision { get; set; }

    /// <summary>Lets widgets sit on top of each other. Disables all collision handling.</summary>
    [Parameter] public bool AllowOverlap { get; set; }

    /// <summary>Accepts HTML5 drags from outside the canvas, such as a widget palette.</summary>
    [Parameter] public bool AcceptExternalDrops { get; set; }

    /// <summary>Id of the selected widget. Supports <c>@bind-SelectedId</c>.</summary>
    [Parameter] public string? SelectedId { get; set; }

    [Parameter] public EventCallback<string?> SelectedIdChanged { get; set; }

    /// <summary>Extra CSS classes for the canvas element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Extra inline styles for the canvas element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Renders the contents of a widget.</summary>
    [Parameter] public RenderFragment<GridItem>? ItemTemplate { get; set; }

    /// <summary>Replaces the title area of the built-in header.</summary>
    [Parameter] public RenderFragment<GridItem>? HeaderTemplate { get; set; }

    /// <summary>Extra buttons in the header, to the left of the remove button.</summary>
    [Parameter] public RenderFragment<GridItem>? ItemActionsTemplate { get; set; }

    /// <summary>Shown when the canvas has no widgets.</summary>
    [Parameter] public RenderFragment? EmptyTemplate { get; set; }

    /// <summary>Fires after any change to widget positions or sizes.</summary>
    [Parameter] public EventCallback<GridLayoutChangedEventArgs> OnLayoutChanged { get; set; }

    /// <summary>Fires when the user starts dragging or resizing.</summary>
    [Parameter] public EventCallback<GridInteractionEventArgs> OnInteractionStart { get; set; }

    /// <summary>Fires when the user finishes dragging or resizing.</summary>
    [Parameter] public EventCallback<GridInteractionEventArgs> OnInteractionEnd { get; set; }

    /// <summary>Fires when a widget is removed from the canvas.</summary>
    [Parameter] public EventCallback<GridItem> OnItemRemoved { get; set; }

    /// <summary>Fires when a widget is added to the canvas.</summary>
    [Parameter] public EventCallback<GridItem> OnItemAdded { get; set; }

    /// <summary>
    /// Fires when something is dropped onto the canvas from outside. Set
    /// <see cref="GridExternalDropEventArgs.Item"/> to insert a widget at the drop point.
    /// </summary>
    [Parameter] public EventCallback<GridExternalDropEventArgs> OnExternalDrop { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>The live layout. Read-only view of <see cref="Items"/>.</summary>
    public IReadOnlyList<GridItem> Layout => new ReadOnlyCollection<GridItem>(_items);

    /// <summary>What the user is doing right now.</summary>
    public GridInteraction CurrentInteraction => _interaction;

    private int EffectiveColumns => Math.Max(1, Columns);

    private bool CanDrag(GridItem item) => EditMode && Draggable && item.Draggable && !item.Static;

    private bool CanResize(GridItem item) => EditMode && Resizable && item.Resizable && !item.Static;

    private bool CanRemove(GridItem item) => EditMode && Removable && item.Removable;

    private string GridCssClass
    {
        get
        {
            var classes = new List<string>(5) { "tc-grid" };
            if (EditMode)
            {
                classes.Add("tc-grid--edit");
            }

            if (_interaction != GridInteraction.None)
            {
                classes.Add("tc-grid--interacting");
                classes.Add(_interaction == GridInteraction.Drag ? "tc-grid--dragging" : "tc-grid--resizing");
            }

            if (!string.IsNullOrWhiteSpace(Class))
            {
                classes.Add(Class!);
            }

            return string.Join(' ', classes);
        }
    }

    private string GridStyle
    {
        get
        {
            // Two extra rows of headroom while dragging so a widget can be dropped past the
            // current bottom edge without the canvas clipping the pointer.
            var rows = Math.Max(GridLayoutEngine.RowCount(_items), Math.Max(0, MinRows));
            if (_interaction != GridInteraction.None)
            {
                rows += 2;
            }

            var height = rows * (RowHeight + Gap) - Gap;
            var css = string.Create(CultureInfo.InvariantCulture,
                $"--tc-cols:{EffectiveColumns};--tc-row-h:{RowHeight}px;--tc-gap:{Gap}px;height:{Math.Max(height, 0)}px;");

            return string.IsNullOrWhiteSpace(Style) ? css : css + Style;
        }
    }

    private string ItemCssClass(GridItem item, bool isActive)
    {
        var classes = new List<string>(5) { "tc-item" };
        if (isActive)
        {
            classes.Add("tc-item--active");
        }

        if (item.Static)
        {
            classes.Add("tc-item--static");
        }

        if (SelectedId == item.Id)
        {
            classes.Add("tc-item--selected");
        }

        if (!string.IsNullOrWhiteSpace(item.CssClass))
        {
            classes.Add(item.CssClass!);
        }

        return string.Join(' ', classes);
    }

    private static string ItemLabel(GridItem item) =>
        item.Title ?? item.Type ?? "Widget";

    // Returning null makes Blazor omit the attribute entirely, which is what the gesture
    // layer looks for when deciding whether an element can start a drag.
    private static string? DragFlag(bool enabled) => enabled ? "1" : null;

    private static string CellVars(GridItem item) =>
        $"--tc-x:{item.X};--tc-y:{item.Y};--tc-w:{item.W};--tc-h:{item.H};";

    private IEnumerable<string> EnabledResizeHandles()
    {
        var handles = ResizeHandles;
        if (handles.HasFlag(TwelveColumns.ResizeHandles.North)) yield return "n";
        if (handles.HasFlag(TwelveColumns.ResizeHandles.East)) yield return "e";
        if (handles.HasFlag(TwelveColumns.ResizeHandles.South)) yield return "s";
        if (handles.HasFlag(TwelveColumns.ResizeHandles.West)) yield return "w";
        if (handles.HasFlag(TwelveColumns.ResizeHandles.NorthEast)) yield return "ne";
        if (handles.HasFlag(TwelveColumns.ResizeHandles.SouthEast)) yield return "se";
        if (handles.HasFlag(TwelveColumns.ResizeHandles.SouthWest)) yield return "sw";
        if (handles.HasFlag(TwelveColumns.ResizeHandles.NorthWest)) yield return "nw";
    }

    protected override void OnParametersSet()
    {
        var incoming = Items ?? _fallbackItems;
        var listChanged = !ReferenceEquals(incoming, _items);
        _items = incoming;

        var columnsChanged = _lastColumns != 0 && _lastColumns != EffectiveColumns;
        _lastColumns = EffectiveColumns;

        if (listChanged || columnsChanged)
        {
            GridLayoutEngine.Normalize(_items, EffectiveColumns, Compaction, AllowOverlap);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            _selfRef = DotNetObjectReference.Create(this);
            _lastJsOptions = JsOptionsKey();
            await _module.InvokeVoidAsync("init", _element, _selfRef, JsOptions());
            _jsInitialized = true;
            return;
        }

        // A drag re-renders on every cell change, so only push options across the wire when
        // one of them has actually changed — otherwise this becomes an interop call per frame.
        if (!_jsInitialized || _module is null)
        {
            return;
        }

        var key = JsOptionsKey();
        if (key == _lastJsOptions)
        {
            return;
        }

        _lastJsOptions = key;
        await _module.InvokeVoidAsync("update", _element, JsOptions());
    }

    private string JsOptionsKey() =>
        $"{EffectiveColumns}/{RowHeight}/{Gap}/{AcceptExternalDrops && EditMode}/{EditMode}";

    private object JsOptions() => new
    {
        columns = EffectiveColumns,
        rowHeight = RowHeight,
        gap = Gap,
        acceptExternalDrops = AcceptExternalDrops && EditMode,
        enabled = EditMode,
    };

    // ---------------------------------------------------------------- JS callbacks

    [JSInvokable]
    public async Task JsInteractionStart(string id, string mode)
    {
        var item = GetItem(id);
        if (item is null)
        {
            return;
        }

        _activeItem = item;
        _interaction = mode == "resize" ? GridInteraction.Resize : GridInteraction.Drag;
        await SelectAsync(id);
        await OnInteractionStart.InvokeAsync(new GridInteractionEventArgs(_interaction, item));
        StateHasChanged();
    }

    [JSInvokable]
    public async Task JsDragMove(string id, int x, int y)
    {
        var item = GetItem(id);
        if (item is null || !CanDrag(item))
        {
            return;
        }

        if (GridLayoutEngine.TryMove(_items, item, x, y, EffectiveColumns, Compaction, PreventCollision, AllowOverlap))
        {
            await NotifyChangedAsync(GridChangeReason.Drag, item);
        }
    }

    [JSInvokable]
    public async Task JsResizeMove(string id, int x, int y, int w, int h)
    {
        var item = GetItem(id);
        if (item is null || !CanResize(item))
        {
            return;
        }

        if (GridLayoutEngine.TryResize(_items, item, x, y, w, h, EffectiveColumns, Compaction, PreventCollision, AllowOverlap))
        {
            await NotifyChangedAsync(GridChangeReason.Resize, item);
        }
    }

    [JSInvokable]
    public async Task JsInteractionEnd(string id)
    {
        var item = GetItem(id) ?? _activeItem;
        var interaction = _interaction;

        _activeItem = null;
        _interaction = GridInteraction.None;

        if (!AllowOverlap)
        {
            GridLayoutEngine.Compact(_items, EffectiveColumns, Compaction);
        }

        if (item is not null && interaction != GridInteraction.None)
        {
            await OnInteractionEnd.InvokeAsync(new GridInteractionEventArgs(interaction, item));
            await NotifyChangedAsync(interaction == GridInteraction.Resize ? GridChangeReason.Resize : GridChangeReason.Drag, item);
        }

        StateHasChanged();
    }

    [JSInvokable]
    public async Task JsExternalDrop(string payload, int x, int y)
    {
        if (!AcceptExternalDrops || !EditMode || !OnExternalDrop.HasDelegate)
        {
            return;
        }

        var args = new GridExternalDropEventArgs(payload ?? string.Empty, Math.Max(0, x), Math.Max(0, y));
        await OnExternalDrop.InvokeAsync(args);

        if (args.Item is not null)
        {
            args.Item.X = args.X;
            args.Item.Y = args.Y;
            await AddItemAsync(args.Item, autoPlace: false);
        }
    }

    // ---------------------------------------------------------------- Public API

    /// <summary>Looks a widget up by id.</summary>
    public GridItem? GetItem(string id) => _items.FirstOrDefault(i => i.Id == id);

    /// <summary>
    /// Adds a widget. With <paramref name="autoPlace"/> the grid finds the first free slot
    /// that fits, ignoring the widget's own X/Y.
    /// </summary>
    public async Task AddItemAsync(GridItem item, bool autoPlace = true)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (autoPlace)
        {
            var (x, y) = GridLayoutEngine.FindFreeSpot(_items, item.W, item.H, EffectiveColumns);
            item.X = x;
            item.Y = y;
        }

        _items.Add(item);
        GridLayoutEngine.Normalize(_items, EffectiveColumns, Compaction, AllowOverlap);

        await OnItemAdded.InvokeAsync(item);
        await NotifyChangedAsync(GridChangeReason.Add, item);
        StateHasChanged();
    }

    /// <summary>Removes a widget by id. Returns <c>false</c> when the id is unknown.</summary>
    public async Task<bool> RemoveItemAsync(string id)
    {
        var item = GetItem(id);
        if (item is null)
        {
            return false;
        }

        await RemoveItemAsync(item);
        return true;
    }

    /// <summary>Removes a widget.</summary>
    public async Task RemoveItemAsync(GridItem item)
    {
        if (!_items.Remove(item))
        {
            return;
        }

        if (SelectedId == item.Id)
        {
            await SelectAsync(null);
        }

        if (!AllowOverlap)
        {
            GridLayoutEngine.Compact(_items, EffectiveColumns, Compaction);
        }

        await OnItemRemoved.InvokeAsync(item);
        await NotifyChangedAsync(GridChangeReason.Remove, item);
        StateHasChanged();
    }

    /// <summary>Removes every widget.</summary>
    public async Task ClearAsync()
    {
        _items.Clear();
        await SelectAsync(null);
        await NotifyChangedAsync(GridChangeReason.Remove, null);
        StateHasChanged();
    }

    /// <summary>Moves a widget programmatically, shuffling neighbours the same way a drag would.</summary>
    public async Task<bool> MoveItemAsync(string id, int x, int y)
    {
        var item = GetItem(id);
        if (item is null ||
            !GridLayoutEngine.TryMove(_items, item, x, y, EffectiveColumns, Compaction, PreventCollision, AllowOverlap))
        {
            return false;
        }

        await NotifyChangedAsync(GridChangeReason.Programmatic, item);
        StateHasChanged();
        return true;
    }

    /// <summary>Resizes a widget programmatically.</summary>
    public async Task<bool> ResizeItemAsync(string id, int w, int h)
    {
        var item = GetItem(id);
        if (item is null ||
            !GridLayoutEngine.TryResize(_items, item, item.X, item.Y, w, h, EffectiveColumns, Compaction, PreventCollision, AllowOverlap))
        {
            return false;
        }

        await NotifyChangedAsync(GridChangeReason.Programmatic, item);
        StateHasChanged();
        return true;
    }

    /// <summary>Re-applies gravity to the whole canvas.</summary>
    public async Task CompactAsync()
    {
        GridLayoutEngine.Compact(_items, EffectiveColumns, Compaction);
        await NotifyChangedAsync(GridChangeReason.Compact, null);
        StateHasChanged();
    }

    /// <summary>Serializes the layout to JSON.</summary>
    public string ExportJson() => GridLayoutSerializer.Serialize(_items);

    /// <summary>Replaces the layout with one previously produced by <see cref="ExportJson"/>.</summary>
    public async Task ImportJsonAsync(string json)
    {
        var restored = GridLayoutSerializer.Deserialize(json);
        _items.Clear();
        foreach (var item in restored)
        {
            _items.Add(item);
        }

        GridLayoutEngine.Normalize(_items, EffectiveColumns, Compaction, AllowOverlap);
        await SelectAsync(null);
        await NotifyChangedAsync(GridChangeReason.Programmatic, null);
        StateHasChanged();
    }

    /// <summary>The first free slot that fits a widget of the given size.</summary>
    public (int X, int Y) FindFreeSpot(int w, int h) =>
        GridLayoutEngine.FindFreeSpot(_items, w, h, EffectiveColumns);

    // ---------------------------------------------------------------- Internals

    private async Task SelectAsync(string? id)
    {
        if (SelectedId == id)
        {
            return;
        }

        SelectedId = id;
        await SelectedIdChanged.InvokeAsync(id);
    }

    private async Task NotifyChangedAsync(GridChangeReason reason, GridItem? item)
    {
        await ItemsChanged.InvokeAsync(_items);
        await OnLayoutChanged.InvokeAsync(new GridLayoutChangedEventArgs(reason, _items.ToList(), item));
        StateHasChanged();
    }

    private async Task OnItemKeyDownAsync(KeyboardEventArgs args, GridItem item)
    {
        if (!EditMode)
        {
            return;
        }

        if (args.Key is "Delete" or "Backspace" && CanRemove(item))
        {
            await RemoveItemAsync(item);
            return;
        }

        var (dx, dy) = args.Key switch
        {
            "ArrowLeft" => (-1, 0),
            "ArrowRight" => (1, 0),
            "ArrowUp" => (0, -1),
            "ArrowDown" => (0, 1),
            _ => (0, 0),
        };

        if (dx == 0 && dy == 0)
        {
            return;
        }

        // Shift turns the arrow keys into a resize gesture, matching the drag/resize split.
        if (args.ShiftKey)
        {
            if (CanResize(item))
            {
                await ResizeItemAsync(item.Id, item.W + dx, item.H + dy);
            }

            return;
        }

        if (CanDrag(item))
        {
            await MoveItemAsync(item.Id, item.X + dx, item.Y + dy);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose", _element);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is already gone; nothing to clean up on the client.
            }
            catch (JSException)
            {
                // The element was torn down before the module could detach from it.
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _selfRef?.Dispose();
    }
}
