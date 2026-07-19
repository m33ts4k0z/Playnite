using Playnite.SDK.Plugins;

namespace Playnite.SDK.V7.Host;

public sealed class V7SearchSupportInstance
{
    public string DefaultKeyword { get; }
    public string Name { get; }
    public object Context { get; }

    public V7SearchSupportInstance(SearchSupport support)
    {
        ArgumentNullException.ThrowIfNull(support);
        DefaultKeyword = support.DefaultKeyword;
        Name = support.Name;
        Context = new V7SearchContextInstance(support.Context);
    }
}

public sealed class V7SearchContextInstance
{
    private readonly SearchContext context;

    public string Description => context.Description;
    public string Hint => context.Hint;
    public string Label => context.Label;
    public int Delay => context.Delay;
    public bool UseAutoSearch => context.UseAutoSearch;
    public bool CacheAutoSearchResults => context.CacheAutoSearchResults;

    public V7SearchContextInstance(SearchContext context) =>
        this.context = context ?? throw new ArgumentNullException(nameof(context));

    public V7SearchResultBatch GetSearchResults(
        string searchTerm,
        bool includeUninstalled,
        bool includeHidden,
        CancellationToken cancellationToken)
    {
        SearchContext switchedContext = null;
        var args = new GetSearchResultsArgs
        {
            SearchTerm = searchTerm ?? string.Empty,
            CancelToken = cancellationToken,
            GameFilterSettings = new GameSearchFilterSettings
            {
                Uninstalled = includeUninstalled,
                Hidden = includeHidden
            },
            SwitchContextAction = context => switchedContext = context
                ?? throw new ArgumentNullException(nameof(context))
        };
        var items = context.GetSearchResults(args)?.ToList() ?? [];
        return new V7SearchResultBatch(
            items.Select(item => new V7SearchItemInstance(item)).ToArray(),
            switchedContext == null ? null : new V7SearchContextInstance(switchedContext));
    }
}

public sealed class V7SearchResultBatch
{
    public object[] Items { get; }
    public object SwitchedContext { get; }

    public V7SearchResultBatch(object[] items, object switchedContext)
    {
        Items = items ?? [];
        SwitchedContext = switchedContext;
    }
}

public sealed class V7SearchItemInstance
{
    public string Name { get; }
    public string Description { get; }
    public object Icon { get; }
    public object PrimaryAction { get; }
    public object SecondaryAction { get; }
    public object MenuAction { get; }

    public V7SearchItemInstance(SearchItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Name = item.Name;
        Description = item.Description;
        Icon = item.Icon;
        PrimaryAction = WrapAction(item.PrimaryAction);
        SecondaryAction = WrapAction(item.SecondaryAction);
        MenuAction = WrapAction(item.MenuAction);
    }

    private static object WrapAction(SearchItemAction action) =>
        action == null ? null : new V7SearchActionInstance(action);
}

public sealed class V7SearchActionInstance
{
    private readonly SearchItemAction action;

    public string Name => action.Name;
    public bool CloseSearch => action.CloseSearch;
    public object Context => action is ContextSwitchSearchItemAction contextSwitch
        ? new V7SearchContextInstance(contextSwitch.Context)
        : null;

    public V7SearchActionInstance(SearchItemAction action) =>
        this.action = action ?? throw new ArgumentNullException(nameof(action));

    public void Invoke()
    {
        if (action is ContextSwitchSearchItemAction)
        {
            throw new InvalidOperationException(
                "Context-switch search actions must be handled through their Context property.");
        }

        (action.Action ?? throw new InvalidOperationException(
            $"Search action '{action.Name}' has no callback."))();
    }
}
