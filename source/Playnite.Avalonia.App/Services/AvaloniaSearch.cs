using Avalonia.Threading;
using Playnite.Avalonia.App.ViewModels;
using Playnite.Common;
using Playnite.SDK.Plugins;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Windows.Input;

namespace Playnite.Avalonia.App.Services;

public sealed class AvaloniaSearchRequest
{
    public string SearchTerm { get; init; }
    public bool IncludeUninstalled { get; init; }
    public bool IncludeHidden { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

public sealed class AvaloniaSearchBatch
{
    public IReadOnlyList<AvaloniaSearchItem> Items { get; }
    public AvaloniaSearchContext SwitchedContext { get; }

    public AvaloniaSearchBatch(
        IReadOnlyList<AvaloniaSearchItem> items,
        AvaloniaSearchContext switchedContext = null)
    {
        Items = items ?? [];
        SwitchedContext = switchedContext;
    }
}

public sealed class AvaloniaSearchContext
{
    private readonly Func<AvaloniaSearchRequest, AvaloniaSearchBatch> search;

    public string Description { get; }
    public string Hint { get; }
    public string Label { get; }
    public int Delay { get; }
    public bool UseAutoSearch { get; }
    public bool CacheAutoSearchResults { get; }

    public AvaloniaSearchContext(
        string description,
        string hint,
        string label,
        int delay,
        bool useAutoSearch,
        bool cacheAutoSearchResults,
        Func<AvaloniaSearchRequest, AvaloniaSearchBatch> search)
    {
        if (delay < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }

        Description = description;
        Hint = hint;
        Label = label;
        Delay = delay;
        UseAutoSearch = useAutoSearch;
        CacheAutoSearchResults = cacheAutoSearchResults;
        this.search = search ?? throw new ArgumentNullException(nameof(search));
    }

    public AvaloniaSearchBatch GetSearchResults(AvaloniaSearchRequest request) =>
        search(request ?? throw new ArgumentNullException(nameof(request)))
        ?? throw new InvalidDataException("A plugin search context returned no result batch.");

    public static AvaloniaSearchContext FromSdk(SearchContext context) =>
        V6SearchContextAdapter.Create(context);

    public static AvaloniaSearchContext FromSdkV7(object context) =>
        V7SearchContextAdapter.Create(context);
}

public sealed class AvaloniaSearchItem
{
    public string Name { get; }
    public string Description { get; }
    public object Icon { get; }
    public string IconPath { get; }
    public bool HasIconPath => !string.IsNullOrWhiteSpace(IconPath);
    public AvaloniaSearchAction PrimaryAction { get; }
    public AvaloniaSearchAction SecondaryAction { get; }
    public AvaloniaSearchAction MenuAction { get; }

    public AvaloniaSearchItem(
        string name,
        string description,
        object icon,
        AvaloniaSearchAction primaryAction,
        AvaloniaSearchAction secondaryAction,
        AvaloniaSearchAction menuAction,
        string iconPath = null)
    {
        Name = name;
        Description = description;
        Icon = icon;
        IconPath = iconPath;
        PrimaryAction = primaryAction;
        SecondaryAction = secondaryAction;
        MenuAction = menuAction;
    }
}

public sealed class AvaloniaSearchAction
{
    private readonly Action invoke;

    public string Name { get; }
    public bool CloseSearch { get; }
    public AvaloniaSearchContext Context { get; }

    public AvaloniaSearchAction(
        string name,
        bool closeSearch,
        AvaloniaSearchContext context,
        Action invoke)
    {
        Name = name;
        CloseSearch = closeSearch;
        Context = context;
        this.invoke = invoke;
    }

    public void Invoke()
    {
        if (Context != null)
        {
            throw new InvalidOperationException(
                "Context-switch search actions must be opened through their Context property.");
        }

        (invoke ?? throw new InvalidOperationException(
            $"Search action '{Name}' has no callback."))();
    }
}

internal static class V6SearchContextAdapter
{
    public static AvaloniaSearchContext Create(SearchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new AvaloniaSearchContext(
            context.Description,
            context.Hint,
            context.Label,
            context.Delay,
            context.UseAutoSearch,
            context.CacheAutoSearchResults,
            request => ConvertBatch(context, request));
    }

    private static AvaloniaSearchBatch ConvertBatch(
        SearchContext context,
        AvaloniaSearchRequest request)
    {
        SearchContext switchedContext = null;
        var arguments = new GetSearchResultsArgs
        {
            SearchTerm = request.SearchTerm ?? string.Empty,
            CancelToken = request.CancellationToken,
            GameFilterSettings = new GameSearchFilterSettings
            {
                Uninstalled = request.IncludeUninstalled,
                Hidden = request.IncludeHidden
            },
            SwitchContextAction = value => switchedContext = value
        };
        var items = (context.GetSearchResults(arguments) ?? Array.Empty<SearchItem>())
            .Where(item => item != null)
            .Select(ConvertItem)
            .ToList();
        return new AvaloniaSearchBatch(
            items,
            switchedContext == null ? null : Create(switchedContext));
    }

    private static AvaloniaSearchItem ConvertItem(SearchItem item) => new(
        item.Name,
        item.Description,
        item.Icon,
        ConvertAction(item.PrimaryAction),
        ConvertAction(item.SecondaryAction),
        ConvertAction(item.MenuAction));

    private static AvaloniaSearchAction ConvertAction(SearchItemAction action)
    {
        if (action == null)
        {
            return null;
        }

        var context = action is ContextSwitchSearchItemAction contextAction
            ? Create(contextAction.Context)
            : null;
        return new AvaloniaSearchAction(
            action.Name,
            action.CloseSearch,
            context,
            action.Action);
    }
}

internal static class V7SearchContextAdapter
{
    public static AvaloniaSearchContext Create(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var type = instance.GetType();
        var getResults = GetRequiredMethod(type, "GetSearchResults");
        return new AvaloniaSearchContext(
            ReadProperty<string>(instance, "Description"),
            ReadProperty<string>(instance, "Hint"),
            ReadProperty<string>(instance, "Label"),
            ReadProperty<int>(instance, "Delay"),
            ReadProperty<bool>(instance, "UseAutoSearch"),
            ReadProperty<bool>(instance, "CacheAutoSearchResults"),
            request => ConvertBatch(Invoke(
                instance,
                getResults,
                request.SearchTerm ?? string.Empty,
                request.IncludeUninstalled,
                request.IncludeHidden,
                request.CancellationToken)));
    }

    private static AvaloniaSearchBatch ConvertBatch(object batch)
    {
        if (batch == null)
        {
            throw new InvalidDataException("SDK v7 search context returned no result batch.");
        }

        var items = ReadProperty<object[]>(batch, "Items")
            .Select(ConvertItem)
            .ToList();
        var switched = ReadProperty<object>(batch, "SwitchedContext");
        return new AvaloniaSearchBatch(items, switched == null ? null : Create(switched));
    }

    private static AvaloniaSearchItem ConvertItem(object item)
    {
        if (item == null)
        {
            throw new InvalidDataException("SDK v7 search context returned a null item.");
        }

        return new AvaloniaSearchItem(
            ReadProperty<string>(item, "Name"),
            ReadProperty<string>(item, "Description"),
            ReadProperty<object>(item, "Icon"),
            ConvertAction(ReadProperty<object>(item, "PrimaryAction")),
            ConvertAction(ReadProperty<object>(item, "SecondaryAction")),
            ConvertAction(ReadProperty<object>(item, "MenuAction")));
    }

    private static AvaloniaSearchAction ConvertAction(object action)
    {
        if (action == null)
        {
            return null;
        }

        var context = ReadProperty<object>(action, "Context");
        var invoke = GetRequiredMethod(action.GetType(), "Invoke");
        return new AvaloniaSearchAction(
            ReadProperty<string>(action, "Name"),
            ReadProperty<bool>(action, "CloseSearch"),
            context == null ? null : Create(context),
            () => Invoke(action, invoke));
    }

    private static T ReadProperty<T>(object instance, string name)
    {
        var property = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMemberException(instance.GetType().FullName, name);
        return (T)property.GetValue(instance);
    }

    private static MethodInfo GetRequiredMethod(Type type, string name) =>
        type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public)
        ?? throw new MissingMethodException(type.FullName, name);

    private static object Invoke(object instance, MethodInfo method, params object[] arguments)
    {
        try
        {
            return method.Invoke(instance, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}

public sealed class AvaloniaSearchSession : INotifyPropertyChanged, IDisposable
{
    private sealed class ContextState
    {
        public AvaloniaSearchContext Context { get; }
        public IReadOnlyList<AvaloniaSearchItem> AutoSearchCache { get; set; }

        public ContextState(AvaloniaSearchContext context) => Context = context;
    }

    private readonly Stack<ContextState> contexts = [];
    private readonly Func<(bool IncludeUninstalled, bool IncludeHidden)> getFilters;
    private readonly Action<string, Exception> reportError;
    private readonly RelayCommand primaryCommand;
    private readonly RelayCommand secondaryCommand;
    private readonly RelayCommand menuCommand;
    private readonly RelayCommand backCommand;
    private CancellationTokenSource currentSearch;
    private string searchText = string.Empty;
    private bool isVisible;
    private bool isSearching;
    private bool includeUninstalled;
    private bool includeHidden;
    private AvaloniaSearchItem selectedResult;
    private int generation;
    private bool disposed;

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler FiltersChanged;

    public ObservableCollection<AvaloniaSearchItem> Results { get; } = [];
    public ICommand PrimaryCommand => primaryCommand;
    public ICommand SecondaryCommand => secondaryCommand;
    public ICommand MenuCommand => menuCommand;
    public ICommand BackCommand => backCommand;
    public ICommand CloseCommand { get; }

    public bool IsVisible
    {
        get => isVisible;
        private set => SetField(ref isVisible, value);
    }

    public bool IsSearching
    {
        get => isSearching;
        private set => SetField(ref isSearching, value);
    }

    public bool IncludeUninstalled
    {
        get => includeUninstalled;
        set => SetFilter(ref includeUninstalled, value, nameof(IncludeUninstalled));
    }

    public bool IncludeHidden
    {
        get => includeHidden;
        set => SetFilter(ref includeHidden, value, nameof(IncludeHidden));
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetField(ref searchText, value ?? string.Empty) && IsVisible)
            {
                _ = RefreshAsync(false);
            }
        }
    }

    public AvaloniaSearchItem SelectedResult
    {
        get => selectedResult;
        set
        {
            if (SetField(ref selectedResult, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string Label => CurrentContext?.Label;
    public string Description => CurrentContext?.Description;
    public string Hint => CurrentContext?.Hint;
    public string PrimaryActionName => SelectedResult?.PrimaryAction?.Name;
    public string SecondaryActionName => SelectedResult?.SecondaryAction?.Name;
    public string MenuActionName => SelectedResult?.MenuAction?.Name;
    public bool HasPrimaryAction => SelectedResult?.PrimaryAction != null;
    public bool HasSecondaryAction => SelectedResult?.SecondaryAction != null;
    public bool HasMenuAction => SelectedResult?.MenuAction != null;
    public bool CanGoBack => contexts.Count > 1;

    private AvaloniaSearchContext CurrentContext => contexts.TryPeek(out var state)
        ? state.Context
        : null;

    public AvaloniaSearchSession(
        Func<(bool IncludeUninstalled, bool IncludeHidden)> getFilters,
        Action<string, Exception> reportError)
    {
        this.getFilters = getFilters ?? throw new ArgumentNullException(nameof(getFilters));
        this.reportError = reportError ?? throw new ArgumentNullException(nameof(reportError));
        primaryCommand = new RelayCommand(() => Invoke(SelectedResult.PrimaryAction), () => HasPrimaryAction);
        secondaryCommand = new RelayCommand(() => Invoke(SelectedResult.SecondaryAction), () => HasSecondaryAction);
        menuCommand = new RelayCommand(() => Invoke(SelectedResult.MenuAction), () => HasMenuAction);
        backCommand = new RelayCommand(GoBack, () => CanGoBack);
        CloseCommand = new RelayCommand(Close);
    }

    public void Open(AvaloniaSearchContext context, string initialSearchTerm)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(context);
        CancelCurrentSearch();
        contexts.Clear();
        contexts.Push(new ContextState(context));
        searchText = initialSearchTerm ?? string.Empty;
        OnPropertyChanged(nameof(SearchText));
        var filters = getFilters();
        includeUninstalled = filters.IncludeUninstalled;
        includeHidden = filters.IncludeHidden;
        OnPropertyChanged(nameof(IncludeUninstalled));
        OnPropertyChanged(nameof(IncludeHidden));
        IsVisible = true;
        NotifyContextChanged();
        _ = RefreshAsync(true);
    }

    public async Task RefreshAsync(bool immediate)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var state = contexts.TryPeek(out var current) ? current : null;
        if (state == null)
        {
            return;
        }

        CancelCurrentSearch();
        var cancellation = new CancellationTokenSource();
        currentSearch = cancellation;
        var searchGeneration = ++generation;
        IsSearching = true;
        try
        {
            if (!immediate && state.Context.Delay > 0)
            {
                await Task.Delay(state.Context.Delay, cancellation.Token);
            }

            var filters = (IncludeUninstalled, IncludeHidden);
            var term = SearchText;
            var batch = await Task.Run(
                () => GetResults(state, term, filters, cancellation.Token),
                cancellation.Token);
            if (cancellation.IsCancellationRequested || generation != searchGeneration)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (batch.SwitchedContext != null)
                {
                    PushContext(batch.SwitchedContext);
                    return;
                }

                Results.Clear();
                foreach (var item in batch.Items)
                {
                    Results.Add(item);
                }
                SelectedResult = Results.FirstOrDefault();
            });
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Results.Clear();
                SelectedResult = null;
                reportError("Plugin search failed.", exception);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ReferenceEquals(currentSearch, cancellation))
                {
                    currentSearch = null;
                    IsSearching = false;
                }
            });
            cancellation.Dispose();
        }
    }

    public void Close()
    {
        CancelCurrentSearch();
        contexts.Clear();
        Results.Clear();
        SelectedResult = null;
        IsVisible = false;
        IsSearching = false;
        NotifyContextChanged();
    }

    private static AvaloniaSearchBatch GetResults(
        ContextState state,
        string term,
        (bool IncludeUninstalled, bool IncludeHidden) filters,
        CancellationToken cancellationToken)
    {
        var request = new AvaloniaSearchRequest
        {
            SearchTerm = term,
            IncludeUninstalled = filters.IncludeUninstalled,
            IncludeHidden = filters.IncludeHidden,
            CancellationToken = cancellationToken
        };
        if (!state.Context.UseAutoSearch)
        {
            return state.Context.GetSearchResults(request);
        }

        if (!state.Context.CacheAutoSearchResults)
        {
            var batch = state.Context.GetSearchResults(request);
            return batch.SwitchedContext == null
                ? new AvaloniaSearchBatch(Filter(batch.Items, term))
                : batch;
        }

        if (state.AutoSearchCache == null)
        {
            var batch = state.Context.GetSearchResults(request);
            if (batch.SwitchedContext != null)
            {
                return batch;
            }
            state.AutoSearchCache = batch.Items;
        }

        return new AvaloniaSearchBatch(Filter(state.AutoSearchCache, term));
    }

    private static IReadOnlyList<AvaloniaSearchItem> Filter(
        IReadOnlyList<AvaloniaSearchItem> items,
        string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return items;
        }

        return items.Where(item => Matches(item.Name, term)).ToList();
    }

    private static bool Matches(string value, string term)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        if (value.Contains(term, StringComparison.CurrentCultureIgnoreCase))
        {
            return true;
        }

        if (term.GetJaroWinklerSimilarityIgnoreCase(value) >= 0.90)
        {
            return true;
        }

        if (term.Length > value.Length)
        {
            return false;
        }

        if (term.IsStartOfStringAcronym(value))
        {
            return true;
        }

        var searchWords = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var valueWords = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return searchWords.All(searchWord => valueWords.Any(valueWord =>
            valueWord.ContainsInvariantCulture(
                searchWord,
                CompareOptions.IgnoreCase |
                CompareOptions.IgnoreSymbols |
                CompareOptions.IgnoreNonSpace)));
    }

    private void Invoke(AvaloniaSearchAction action)
    {
        try
        {
            if (action.Context != null)
            {
                PushContext(action.Context);
                return;
            }

            if (action.CloseSearch)
            {
                Close();
            }
            action.Invoke();
        }
        catch (Exception exception)
        {
            reportError($"Plugin search action '{action.Name}' failed.", exception);
        }
    }

    private void PushContext(AvaloniaSearchContext context)
    {
        contexts.Push(new ContextState(context));
        searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        NotifyContextChanged();
        _ = RefreshAsync(true);
    }

    private void GoBack()
    {
        if (contexts.Count <= 1)
        {
            return;
        }

        contexts.Pop();
        searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        NotifyContextChanged();
        _ = RefreshAsync(true);
    }

    private void CancelCurrentSearch()
    {
        currentSearch?.Cancel();
        currentSearch = null;
    }

    private void NotifyContextChanged()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(CanGoBack));
        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        OnPropertyChanged(nameof(PrimaryActionName));
        OnPropertyChanged(nameof(SecondaryActionName));
        OnPropertyChanged(nameof(MenuActionName));
        OnPropertyChanged(nameof(HasPrimaryAction));
        OnPropertyChanged(nameof(HasSecondaryAction));
        OnPropertyChanged(nameof(HasMenuAction));
        primaryCommand.RaiseCanExecuteChanged();
        secondaryCommand.RaiseCanExecuteChanged();
        menuCommand.RaiseCanExecuteChanged();
        backCommand.RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void SetFilter(ref bool field, bool value, string propertyName)
    {
        if (!SetField(ref field, value, propertyName))
        {
            return;
        }

        FiltersChanged?.Invoke(this, EventArgs.Empty);
        if (IsVisible)
        {
            _ = RefreshAsync(false);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Close();
        disposed = true;
    }
}
