using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Playnite.DesktopApp.Avalonia.Controls;

public sealed class DesktopWindowChrome : ContentControl
{
    private readonly MainWindow window;
    private Border titleBar;
    private Button minimizeButton;
    private Button maximizeButton;
    private Button closeButton;

    internal int TemplateAppliedCount { get; private set; }
    internal Border TitleBar => titleBar;
    internal Button MinimizeButton => minimizeButton;
    internal Button MaximizeButton => maximizeButton;
    internal Button CloseButton => closeButton;

    public DesktopWindowChrome(MainWindow window)
    {
        this.window = window ?? throw new ArgumentNullException(nameof(window));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        TemplateAppliedCount++;

        titleBar = e.NameScope.Find<Border>("PART_TitleBar");
        minimizeButton = e.NameScope.Find<Button>("PART_MinimizeButton");
        maximizeButton = e.NameScope.Find<Button>("PART_MaximizeButton");
        closeButton = e.NameScope.Find<Button>("PART_CloseButton");

        if (titleBar != null)
        {
            WindowDecorationProperties.SetElementRole(
                titleBar,
                WindowDecorationsElementRole.TitleBar);
        }

        ConfigureButton(minimizeButton, WindowDecorationsElementRole.MinimizeButton, Minimize);
        ConfigureButton(maximizeButton, WindowDecorationsElementRole.MaximizeButton, ToggleMaximize);
        ConfigureButton(closeButton, WindowDecorationsElementRole.CloseButton, RequestClose);
    }

    internal void Minimize() => window.Minimize();

    internal void ToggleMaximize() =>
        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    internal void RequestClose() => window.Close();

    private static void ConfigureButton(
        Button button,
        WindowDecorationsElementRole role,
        Action action)
    {
        if (button == null)
        {
            return;
        }

        WindowDecorationProperties.SetElementRole(button, role);
        button.Click += (_, _) => action();
    }
}
