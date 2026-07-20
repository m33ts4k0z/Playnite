using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.Common;
using Playnite.Common.Media.Icons;
using Playnite.Database;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.SDK.Models;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopToolsConfigViewModel : INotifyPropertyChanged
{
    private readonly GameDatabase database;
    private readonly Func<DesktopDialogService> dialogs;
    private readonly Action saved;
    private bool isVisible;
    private AppSoftware selectedApp;

    public event PropertyChangedEventHandler PropertyChanged;
    public ObservableCollection<AppSoftware> EditingApps { get; } = new();
    public IReadOnlyList<AppSoftwareType> AppTypes { get; } = Enum.GetValues<AppSoftwareType>();
    public bool IsVisible { get => isVisible; private set => SetField(ref isVisible, value); }

    public AppSoftware SelectedApp
    {
        get => selectedApp;
        set
        {
            if (SetField(ref selectedApp, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public ICommand AddCommand { get; }
    public ICommand AddFromFileCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand SelectIconCommand { get; }
    public ICommand RemoveIconCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public DesktopToolsConfigViewModel(
        GameDatabase database,
        Func<DesktopDialogService> dialogs,
        Action saved)
    {
        this.database = database;
        this.dialogs = dialogs ?? (() => null);
        this.saved = saved ?? (() => { });
        AddCommand = new AppRelayCommand(Add, () => IsVisible && database != null);
        AddFromFileCommand = new AppRelayCommand(AddFromFile, () => IsVisible && database != null);
        RemoveCommand = new AppRelayCommand(Remove, () => IsVisible && SelectedApp != null);
        SelectIconCommand = new AppRelayCommand(SelectIcon, () => IsVisible && SelectedApp != null);
        RemoveIconCommand = new AppRelayCommand(
            () => SelectedApp.Icon = null,
            () => IsVisible && SelectedApp != null && !string.IsNullOrWhiteSpace(SelectedApp.Icon));
        SaveCommand = new AppRelayCommand(Save, () => IsVisible && database != null);
        CancelCommand = new AppRelayCommand(Close, () => IsVisible);
    }

    public bool Open()
    {
        if (database == null || IsVisible)
        {
            return false;
        }

        EditingApps.Clear();
        foreach (var app in database.SoftwareApps.GetClone()
            .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            EditingApps.Add(app);
        }

        SelectedApp = EditingApps.FirstOrDefault();
        IsVisible = true;
        RaiseCommandStates();
        return true;
    }

    public void Close()
    {
        IsVisible = false;
        EditingApps.Clear();
        SelectedApp = null;
        RaiseCommandStates();
    }

    private void Add()
    {
        var app = new AppSoftware("New tool");
        EditingApps.Add(app);
        SelectedApp = app;
    }

    private void AddFromFile()
    {
        var path = dialogs()?.SelectFiles("Executable or shortcut|*.exe;*.lnk;*.bat", false).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var program = Programs.GetProgramData(path);
        var app = new AppSoftware(string.IsNullOrWhiteSpace(program.Name)
            ? Path.GetFileNameWithoutExtension(path)
            : program.Name)
        {
            Path = program.Path ?? path,
            Arguments = program.Arguments,
            WorkingDir = program.WorkDir
        };
        if (!string.IsNullOrWhiteSpace(program.Icon))
        {
            app.Icon = ExtractIconIfNeeded(program.Icon);
        }

        EditingApps.Add(app);
        SelectedApp = app;
    }

    private void Remove()
    {
        var index = EditingApps.IndexOf(SelectedApp);
        EditingApps.Remove(SelectedApp);
        SelectedApp = EditingApps.Count == 0
            ? null
            : EditingApps[Math.Clamp(index, 0, EditingApps.Count - 1)];
    }

    private void SelectIcon()
    {
        var path = dialogs()?.SelectFiles("Icon or executable|*.ico;*.png;*.jpg;*.jpeg;*.webp;*.exe", false)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(path))
        {
            SelectedApp.Icon = ExtractIconIfNeeded(path);
            ((AppRelayCommand)RemoveIconCommand).RaiseCanExecuteChanged();
        }
    }

    private static string ExtractIconIfNeeded(string path)
    {
        if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var converted = Path.Combine(global::Playnite.PlaynitePaths.TempPath, $"{Guid.NewGuid():N}.ico");
        return IconExtractor.ExtractMainIconFromFile(path, converted) ? converted : null;
    }

    private void Save()
    {
        database.SoftwareApps.BeginBufferUpdate();
        try
        {
            foreach (var app in EditingApps.Where(app => database.SoftwareApps[app.Id] != null).ToList())
            {
                var current = database.SoftwareApps.Get(app.Id);
                if (app.IsEqualJson(current))
                {
                    continue;
                }

                ImportIcon(app, current.Id);
                database.SoftwareApps.Update(app);
            }

            database.SoftwareApps.Remove(database.SoftwareApps
                .Where(app => EditingApps.All(editing => editing.Id != app.Id))
                .ToList());

            foreach (var app in EditingApps.Where(app => database.SoftwareApps[app.Id] == null).ToList())
            {
                ImportIcon(app, app.Id);
                database.SoftwareApps.Add(app);
            }
        }
        finally
        {
            database.SoftwareApps.EndBufferUpdate();
        }

        saved();
        Close();
    }

    private void ImportIcon(AppSoftware app, Guid parentId)
    {
        if (string.IsNullOrWhiteSpace(app.Icon) || !File.Exists(app.Icon))
        {
            return;
        }

        var source = app.Icon;
        app.Icon = database.AddFile(source, parentId, true, CancellationToken.None);
        if (Paths.AreEqual(Path.GetDirectoryName(source), global::Playnite.PlaynitePaths.TempPath))
        {
            FileSystem.DeleteFile(source);
        }
    }

    private void RaiseCommandStates()
    {
        ((AppRelayCommand)AddCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)AddFromFileCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)RemoveCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)SelectIconCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)RemoveIconCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)SaveCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)CancelCommand).RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
