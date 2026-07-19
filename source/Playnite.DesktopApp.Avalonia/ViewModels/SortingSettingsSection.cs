using System.Collections.ObjectModel;
using System.Windows.Input;
using Playnite.Database;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class SortingSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private readonly GameDatabase database;
    private readonly Action libraryUpdated;
    private readonly Action<string, bool> showMessage;
    private bool gameSortingNameAutofill;
    private string articleText = string.Empty;
    private string selectedArticle;

    public override string Key => "Sorting";
    public override string Title => "Library — Sorting";
    public override global::Avalonia.Controls.Control Content { get; }
    public ObservableCollection<string> RemovedArticles { get; } = new();
    public ICommand AddArticleCommand { get; }
    public ICommand RemoveArticleCommand { get; }
    public ICommand FillSortingNamesCommand { get; }

    public bool GameSortingNameAutofill { get => gameSortingNameAutofill; set => SetField(ref gameSortingNameAutofill, value); }
    public string ArticleText
    {
        get => articleText;
        set
        {
            if (SetField(ref articleText, value ?? string.Empty))
            {
                ((AppRelayCommand)AddArticleCommand).RaiseCanExecuteChanged();
            }
        }
    }
    public string SelectedArticle
    {
        get => selectedArticle;
        set
        {
            if (SetField(ref selectedArticle, value))
            {
                ((AppRelayCommand)RemoveArticleCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public SortingSettingsSection(
        DesktopSettings settings,
        GameDatabase database,
        Action libraryUpdated,
        Action<string, bool> showMessage)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.database = database;
        this.libraryUpdated = libraryUpdated ?? (() => { });
        this.showMessage = showMessage ?? ((_, _) => { });
        AddArticleCommand = new AppRelayCommand(AddArticle, CanAddArticle);
        RemoveArticleCommand = new AppRelayCommand(RemoveArticle, () => SelectedArticle != null);
        FillSortingNamesCommand = new AppRelayCommand(FillSortingNames, () => database != null);
        Content = new SortingSettingsView { DataContext = this };
    }

    public override void Open()
    {
        GameSortingNameAutofill = settings.GameSortingNameAutofill;
        RemovedArticles.Clear();
        foreach (var article in (settings.GameSortingNameRemovedArticles ?? new List<string>())
                     .OrderBy(article => article, StringComparer.CurrentCultureIgnoreCase))
        {
            RemovedArticles.Add(article);
        }

        ArticleText = string.Empty;
        SelectedArticle = null;
    }

    public override SettingsSectionValidationResult Validate() =>
        RemovedArticles.Any(article => string.IsNullOrWhiteSpace(article))
            ? new(false, "Sorting-name articles cannot be empty.")
            : SettingsSectionValidationResult.Valid;

    public override SettingsSectionSaveResult Save()
    {
        settings.GameSortingNameAutofill = GameSortingNameAutofill;
        settings.GameSortingNameRemovedArticles = RemovedArticles
            .Select(article => article.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = Content.DataContext == this && Validate().IsValid;
        return new(Key, valid, valid
            ? $"{RemovedArticles.Count} removable articles and buffered bulk autofill are available"
            : "sorting-name settings are invalid");
    }

    private bool CanAddArticle()
    {
        var article = ArticleText.Trim();
        return article.Length > 0 && !RemovedArticles.Contains(article, StringComparer.CurrentCultureIgnoreCase);
    }

    private void AddArticle()
    {
        var article = ArticleText.Trim();
        if (!CanAddArticle())
        {
            return;
        }

        RemovedArticles.Add(article);
        ArticleText = string.Empty;
    }

    private void RemoveArticle()
    {
        if (SelectedArticle == null)
        {
            return;
        }

        RemovedArticles.Remove(SelectedArticle);
        SelectedArticle = null;
    }

    private void FillSortingNames()
    {
        try
        {
            var count = SortingNameService.FillMissing(database, database.Games, RemovedArticles);
            libraryUpdated();
            showMessage($"Filled sorting names for {count:N0} game(s).", false);
        }
        catch (Exception exception)
        {
            showMessage($"Sorting names could not be filled: {exception.Message}", true);
        }
    }
}
