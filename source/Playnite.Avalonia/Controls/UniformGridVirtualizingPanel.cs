using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Playnite.Avalonia.Controls;

/// <summary>
/// Virtualizing uniform tile grid shared by desktop grid view and fullscreen.
/// It realizes only the effective viewport plus one buffered row and recycles
/// containers through Avalonia's ItemContainerGenerator protocol.
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

    private readonly Dictionary<int, Control> realizedByIndex = new();
    private readonly Dictionary<Control, int> indexByControl = new();
    private readonly Dictionary<object, Stack<Control>> recyclePool = new();
    private readonly Dictionary<Control, object> recycleKeyByControl = new();
    private Rect viewport;
    private bool viewportKnown;

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

    static UniformGridVirtualizingPanel()
    {
        AffectsMeasure<UniformGridVirtualizingPanel>(ItemWidthProperty, ItemHeightProperty);
        AffectsArrange<UniformGridVirtualizingPanel>(ItemWidthProperty, ItemHeightProperty);
    }

    public UniformGridVirtualizingPanel()
    {
        EffectiveViewportChanged += (_, args) =>
        {
            var next = args.EffectiveViewport;
            var changed = !viewportKnown ||
                Math.Abs(next.Y - viewport.Y) > 1 ||
                Math.Abs(next.Height - viewport.Height) > 1 ||
                Math.Abs(next.Width - viewport.Width) > 1;
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
            return default;
        }

        var width = !double.IsInfinity(availableSize.Width)
            ? availableSize.Width
            : viewportKnown && viewport.Width > 0 ? viewport.Width : ItemWidth * 5;
        var columns = ColumnsFor(width);
        var rows = (int)Math.Ceiling((double)count / columns);
        var extent = new Size(columns * ItemWidth, rows * ItemHeight);

        var viewTop = viewportKnown && viewport.Height > 0 ? Math.Max(0, viewport.Y) : 0;
        var viewBottom = viewportKnown && viewport.Height > 0
            ? viewport.Bottom
            : !double.IsInfinity(availableSize.Height) ? availableSize.Height : ItemHeight * 3;
        var firstRow = Math.Max(0, (int)((viewTop - ItemHeight) / ItemHeight));
        var lastRow = Math.Min(rows - 1, (int)((viewBottom + ItemHeight) / ItemHeight));
        var firstIndex = firstRow * columns;
        var lastIndex = Math.Min(count - 1, (lastRow + 1) * columns - 1);

        foreach (var index in realizedByIndex.Keys
                     .Where(index => index < firstIndex || index > lastIndex)
                     .ToList())
        {
            RecycleContainer(index);
        }

        var itemSize = new Size(ItemWidth, ItemHeight);
        for (var index = firstIndex; index <= lastIndex; index++)
        {
            GetOrRealizeContainer(index)?.Measure(itemSize);
        }

        return extent;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = ColumnsFor(finalSize.Width);
        foreach (var (index, control) in realizedByIndex)
        {
            var row = index / columns;
            var column = index % columns;
            control.Arrange(new Rect(
                column * ItemWidth,
                row * ItemHeight,
                ItemWidth,
                ItemHeight));
        }

        return finalSize;
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

        var columns = ColumnsFor(Bounds.Width > 0 ? Bounds.Width : ItemWidth * 5);
        var row = index / columns;
        var column = index % columns;
        var rect = new Rect(column * ItemWidth, row * ItemHeight, ItemWidth, ItemHeight);
        var container = GetOrRealizeContainer(index);
        container.Measure(new Size(ItemWidth, ItemHeight));
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

        var columns = ColumnsFor(Bounds.Width > 0 ? Bounds.Width : ItemWidth * 5);
        var fromIndex = from is Control control ? IndexFromContainer(control) : -1;
        var target = direction switch
        {
            NavigationDirection.First => 0,
            NavigationDirection.Last => count - 1,
            NavigationDirection.Next or NavigationDirection.Right => fromIndex + 1,
            NavigationDirection.Previous or NavigationDirection.Left => fromIndex - 1,
            NavigationDirection.Up => fromIndex - columns,
            NavigationDirection.Down => fromIndex + columns,
            NavigationDirection.PageUp => fromIndex - columns * 3,
            NavigationDirection.PageDown => fromIndex + columns * 3,
            _ => -1
        };

        if (fromIndex == -1 &&
            direction != NavigationDirection.First &&
            direction != NavigationDirection.Last)
        {
            target = 0;
        }

        return target >= 0 && target < count ? ScrollIntoView(target) : null;
    }

    private static bool IsValidItemDimension(double value) => double.IsFinite(value) && value > 0;

    private int ColumnsFor(double width) => Math.Max(1, (int)(width / ItemWidth));

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
