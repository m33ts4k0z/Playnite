namespace Playnite.Avalonia.App.Services;

// What the desktop shell does to its window when a game starts. Mirrors the WPF
// AfterLaunchOptions (Desktop mode). Defined shell-side because the WPF enum
// lives in the WPF-only Playnite assembly, which the Avalonia shells do not
// reference.
public enum AfterLaunchOption
{
    None,
    Minimize,
    Close
}

// What the desktop shell does when a game stops. Mirrors the WPF
// AfterGameCloseOptions for the options the shell can honor. RestoreOnlyFromUI
// is intentionally omitted until launch-source tracking exists.
public enum AfterGameCloseOption
{
    None,
    Restore,
    Exit
}
