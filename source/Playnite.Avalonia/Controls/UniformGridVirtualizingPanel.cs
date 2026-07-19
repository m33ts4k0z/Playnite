using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Playnite.Avalonia.Controls;

/// <summary>
/// Virtualizing uniform tile grid shared by desktop grid view and fullscreen.
/// Vertical flow wraps by columns; horizontal flow wraps by rows. Only the
/// effective viewport plus one buffered row/column is realized.
/// </summary>
public class UniformGridVirtualizingPanel : VirtualizingPanel
{
    public static readonly StyledProperty<double> ItemWidthProperty =
        AvaloniaProperty.Register<UniformGridVirtualizingPanel, double>(
            nameof(ItemWidth),
            200,
            validate: IsValidItemDimension);

    public static readonly StyledProperty<double> ItemHeightProperty =
        AvaloniaProperty.Register<UniformGridVirtualizingPanel, double>(
            nameof(ItemHeight),
            300,
            validate: IsValidItemDimension);

    public static readonly StyledProperty<double> ItemAspectRatioProperty =
        AvaloniaProperty.Register<UniformGridVirtualizingPanel, double>(
            nameof(ItemAspectRatio),
            0,
            validate: value => double.IsFinite(value) && value >= 0);

    public static readonly StyledProperty<double> ItemSpacingProperty =
        AvaloniaProperty.Register<UniformGridVirtualizingPanel, double>(
            nameof(ItemSpacing),
            0,
            validate: value => double.IsFinite(value) && value >= 0);

    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<UniformGridVirtualizingPanel, int>(
            nameof(Columns),
            0,
            validate: value => value >= 0);

    public static readonly StyledProperty<int> RowsProperty =
        AvaloniaProperty.Register<UniformGridVirtualizingPanel, int>(
            nameof(Rows),
            0,
            validate: value => value >= 0);

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<UniformGridVirtualizingPanel, Orientation>(
            nameof(Orientation),
            Orientation.Vertical,
            validate: value => Enum.IsDefined(value));

    private readonly Dictionary<int, Control> realizedByIndex = new();
    private readonly Dictionary<Control, int> indexByControl = new();
    private readonly Dictionary<object, Stack<Control>> recyclePool = new();
    private readonly Dictionary<Control, object> recycleKeyByControl = new();
    private Rect viewport;
    private bool viewportKnown;
    private LayoutInfo lastLayout;

    private readonly record struct LayoutInfo(
        int Columns,
        int Rows,
        double ItemWidth,
        double ItemHeight,
        double CellWidth,
        double CellHeight,
        Size Extent);

    public int ContainersCreated { get; private set; }
    public int ContainersReused { get; private set; }
    public int RealizedCount => realizedByIndex.Count;
    public int PooledCount => recyclePool.Values.Sum(pool => pool.Count);

    public double ItemWidth
    {
        get => GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    /// <summary>
    /// Width divided by height. Zero keeps the explicit ItemHeight; a positive
    /// value derives item height from ItemWidth.
    /// </summary>
    public double ItemAspectRatio
    {
        get => GetValue(ItemAspectRatioProperty);
        set => SetValue(ItemAspectRatioProperty, value);
    }

    public double ItemSpacing
    {
        get => GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    /// <summary>Vertical-flow wrap count. Zero derives it from available width.</summary>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>Horizontal-flow wrap count. Zero derives it from available height.</summary>
    public int Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double EffectiveItemWidth => ItemWidth;
    public double EffectiveItemHeight => ItemAspectRatio > 0
        ? ItemWidth / ItemAspectRatio
        : ItemHeight;

    static UniformGridVirtualizingPanel()
    {
        AffectsMeasure<UniformGridVirtualizingPanel>(
            ItemWidthProperty,
            ItemHeightProperty,
            ItemAspectRatioProperty,
            ItemSpacingProperty,
            ColumnsProperty,
            RowsProperty,
            OrientationProperty);
        AffectsArrange<UniformGridVirtualizingPanel>(
            ItemWidthProperty,
            ItemHeightProperty,
            ItemAspectRatioProperty,
            ItemSpacingProperty,
            ColumnsProperty,
            RowsProperty,
            OrientationProperty);
    }

    public UniformGridVirtualizingPanel()
    {
        EffectiveViewportChanged += (_, args) =>
        {
            var next = args.EffectiveViewport;
            var changed = !viewportKnown ||
                Math.Abs(next.X - viewport.X) > 1 ||
                Math.Abs(next.Y - viewport.Y) > 1 ||
                Math.Abs(next.Width - viewport.Width) > 1 ||
                Math.Abs(next.Height - viewport.Height) > 1;
            viewport = next;
            viewportKnown = true;
            if (changed)
            {
                InvalidateMeasure();
            }
        };
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Items.Count;
        if (count == 0 || ItemContainerGenerator is null)
        {
            UnrealizeAll();
            lastLayout = default;
            return default;
        }

        lastLayout = CreateLayout(availableSize, count);
        var (firstIndex, lastIndex) = GetRealizationRange(lastLayout, count, availableSize);
        foreach (var index in realizedByIndex.Keys
                     .Where(index => index < firstIndex || index > lastIndex)
                     .ToList())
        {
            RecycleContainer(index);
        }

        var itemSize = new Size(lastLayout.ItemWidth, lastLayout.ItemHeight);
        for (var index = firstIndex; index <= lastIndex; index++)
        {
            GetOrRealizeContainer(index)?.Measure(itemSize);
        }

        return lastLayout.Extent;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var layout = CreateLayout(finalSize, Items.Count);
        lastLayout = layout;
        foreach (var (index, control) in realizedByIndex)
        {
            control.Arrange(RectFor(index, layout));
        }

        return layout.Extent;
    }

    protected override void OnItemsChanged(
        IReadOnlyList<object> items,
        NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(items, e);
        UnrealizeAll();
        InvalidateMeasure();
    }

    protected override Control ContainerFromIndex(int index) =>
        realizedByIndex.TryGetValue(index, out var container) ? container : null;

    protected override int IndexFromContainer(Control container) =>
        indexByControl.TryGetValue(container, out var index) ? index : -1;

    protected override IEnumerable<Control> GetRealizedContainers() => realizedByIndex.Values;

    protected override Control ScrollIntoView(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            return null;
        }

        var layout = lastLayout.Columns > 0
            ? lastLayout
            : CreateLayout(Bounds.Size, Items.Count);
        var rect = RectFor(index, layout);
        var container = GetOrRealizeContainer(index);
        container.Measure(new Size(layout.ItemWidth, layout.ItemHeight));
        container.Arrange(rect);
        this.BringIntoView(rect);
        return container;
    }

    protected override IInputElement GetControl(
        NavigationDirection direction,
        IInputElement from,
        bool wrap)
    {
        var count = Items.Count;
        if (count == 0)
        {
            return null;
        }

        var layout = lastLayout.Columns > 0
            ? lastLayout
            : CreateLayout(Bounds.Size, count);
        var fromIndex = from is Control control ? IndexFromContainer(control) : -1;
        var rowStep = Orientation == Orientation.Vertical ? layout.Columns : 1;
        var columnStep = Orientation == Orientation.Vertical ? 1 : layout.Rows;
        var pageStep = Orientation == Orientation.Vertical
            ? layout.Columns * Math.Max(1, Math.Min(3, layout.Rows))
            : layout.Rows * Math.Max(1, Math.Min(3, layout.Columns));
        var target = direction switch
        {
            NavigationDirection.First => 0,
            NavigationDirection.Last => count - 1,
            NavigationDirection.Next => fromIndex + 1,
            NavigationDirection.Previous => fromIndex - 1,
            NavigationDirection.Left => fromIndex - columnStep,
            NavigationDirection.Right => fromIndex + columnStep,
            NavigationDirection.Up => fromIndex - rowStep,
            NavigationDirection.Down => fromIndex + rowStep,
            NavigationDirection.PageUp => fromIndex - pageStep,
            NavigationDirection.PageDown => fromIndex + pageStep,
            _ => -1
        };

        if (fromIndex == -1 &&
            direction is not NavigationDirection.First and not NavigationDirection.Last)
        {
            target = 0;
        }

        return target >= 0 && target < count ? ScrollIntoView(target) : null;
    }

    private LayoutInfo CreateLayout(Size availableSize, int count)
    {
        var itemWidth = EffectiveItemWidth;
        var itemHeight = EffectiveItemHeight;
        var cellWidth = itemWidth + ItemSpacing;
        var cellHeight = itemHeight + ItemSpacing;
        int columns;
        int rows;

        if (Orientation == Orientation.Vertical)
        {
            var width = ResolveAvailableLength(
                availableSize.Width,
                viewportKnown ? viewport.Width : 0,
                cellWidth * (Columns > 0 ? Columns : 5));
            columns = Columns > 0
                ? Columns
                : Math.Max(1, (int)((width + ItemSpacing) / cellWidth));
            rows = Math.Max(1, (int)Math.Ceiling((double)Math.Max(1, count) / columns));
        }
        else
        {
            var height = ResolveAvailableLength(
                availableSize.Height,
                viewportKnown ? viewport.Height : 0,
                cellHeight * (Rows > 0 ? Rows : 3));
            rows = Rows > 0
                ? Rows
                : Math.Max(1, (int)((height + ItemSpacing) / cellHeight));
            columns = Math.Max(1, (int)Math.Ceiling((double)Math.Max(1, count) / rows));
        }

        var extent = new Size(
            columns * itemWidth + Math.Max(0, columns - 1) * ItemSpacing,
            rows * itemHeight + Math.Max(0, rows - 1) * ItemSpacing);
        return new LayoutInfo(columns, rows, itemWidth, itemHeight, cellWidth, cellHeight, extent);
    }

    private (int FirstIndex, int LastIndex) GetRealizationRange(
        LayoutInfo layout,
        int count,
        Size availableSize)
    {
        if (Orientation == Orientation.Vertical)
        {
            var viewTop = viewportKnown && viewport.Height > 0 ? Math.Max(0, viewport.Y) : 0;
            var viewBottom = viewportKnown && viewport.Height > 0
                ? viewport.Bottom
                : ResolveAvailableLength(availableSize.Height, 0, layout.CellHeight * 3);
            var firstRow = Math.Max(0, (int)Math.Floor(viewTop / layout.CellHeight) - 1);
            var lastRow = Math.Min(
                layout.Rows - 1,
                (int)Math.Floor(viewBottom / layout.CellHeight) + 1);
            return (
                firstRow * layout.Columns,
                Math.Min(count - 1, (lastRow + 1) * layout.Columns - 1));
        }

        var viewLeft = viewportKnown && viewport.Width > 0 ? Math.Max(0, viewport.X) : 0;
        var viewRight = viewportKnown && viewport.Width > 0
            ? viewport.Right
            : ResolveAvailableLength(availableSize.Width, 0, layout.CellWidth * 3);
        var firstColumn = Math.Max(0, (int)Math.Floor(viewLeft / layout.CellWidth) - 1);
        var lastColumn = Math.Min(
            layout.Columns - 1,
            (int)Math.Floor(viewRight / layout.CellWidth) + 1);
        return (
            firstColumn * layout.Rows,
            Math.Min(count - 1, (lastColumn + 1) * layout.Rows - 1));
    }

    private Rect RectFor(int index, LayoutInfo layout)
    {
        int row;
        int column;
        if (Orientation == Orientation.Vertical)
        {
            row = index / layout.Columns;
            column = index % layout.Columns;
        }
        else
        {
            column = index / layout.Rows;
            row = index % layout.Rows;
        }

        return new Rect(
            column * layout.CellWidth,
            row * layout.CellHeight,
            layout.ItemWidth,
            layout.ItemHeight);
    }

    private static double ResolveAvailableLength(double available, double viewportLength, double fallback) =>
        double.IsFinite(available) && available > 0
            ? available
            : viewportLength > 0 ? viewportLength : fallback;

    private static bool IsValidItemDimension(double value) => double.IsFinite(value) && value > 0;

    private Control GetOrRealizeContainer(int index)
    {
        if (realizedByIndex.TryGetValue(index, out var existing))
        {
            return existing;
        }

        var generator = ItemContainerGenerator;
        var item = Items[index];
        Control container;

        if (generator.NeedsContainer(item, index, out var recycleKey))
        {
            if (recycleKey != null &&
                recyclePool.TryGetValue(recycleKey, out var pool) &&
                pool.Count > 0)
            {
                container = pool.Pop();
                container.SetCurrentValue(Visual.IsVisibleProperty, true);
                generator.PrepareItemContainer(container, item, index);
                AddInternalChild(container);
                generator.ItemContainerPrepared(container, item, index);
                ContainersReused++;
            }
            else
            {
                container = generator.CreateContainer(item, index, recycleKey);
                generator.PrepareItemContainer(container, item, index);
                AddInternalChild(container);
                generator.ItemContainerPrepared(container, item, index);
                ContainersCreated++;
            }

            recycleKeyByControl[container] = recycleKey;
        }
        else
        {
            container = (Control)item;
            container.SetCurrentValue(Visual.IsVisibleProperty, true);
            generator.PrepareItemContainer(container, item, index);
            AddInternalChild(container);
            generator.ItemContainerPrepared(container, item, index);
            recycleKeyByControl[container] = null;
            ContainersCreated++;
        }

        realizedByIndex[index] = container;
        indexByControl[container] = index;
        return container;
    }

    private void RecycleContainer(int index)
    {
        if (!realizedByIndex.Remove(index, out var container))
        {
            return;
        }

        indexByControl.Remove(container);
        recycleKeyByControl.Remove(container, out var recycleKey);

        if (recycleKey is null)
        {
            container.SetCurrentValue(Visual.IsVisibleProperty, false);
            RemoveInternalChild(container);
            return;
        }

        ItemContainerGenerator.ClearItemContainer(container);
        if (!recyclePool.TryGetValue(recycleKey, out var pool))
        {
            pool = new Stack<Control>();
            recyclePool[recycleKey] = pool;
        }

        pool.Push(container);
        container.SetCurrentValue(Visual.IsVisibleProperty, false);
        RemoveInternalChild(container);
    }

    private void UnrealizeAll()
    {
        foreach (var index in realizedByIndex.Keys.ToList())
        {
            RecycleContainer(index);
        }
    }
}
