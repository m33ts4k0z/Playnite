# Playnite SDK 7 plugin migration

SDK 7 moves Playnite's compiled plugin UI contract from WPF to Avalonia and its blocking host calls to asynchronous APIs. SDK 6 remains supported by the compatibility host, so migrate and publish a new plugin build without replacing the working SDK 6 package until the SDK 7 build passes the full gate.

## Check a source tree

Toolbox 3 can inspect a compiled plugin source directory without changing any file:

```powershell
Toolbox.exe migration-check C:\source\MyPlugin
Toolbox.exe migration-check C:\source\MyPlugin --format Json --output migration-report.json
```

Exit code `0` means no static blockers were found. Exit code `1` means the report contains errors or the directory could not be analyzed. JSON reports contain stable diagnostic codes, severity, relative file, and line data for CI. Warnings identify likely legacy calls that need receiver-type confirmation.

The checker validates:

- `net10.0`, `Microsoft.NET.Sdk`, no `UseWPF` or Windows-only target framework;
- compile-only `PlayniteSDK` 7.x and `Avalonia` 12.x references with no host runtime DLL copies;
- warnings-as-errors, NuGet auditing, and nullable-analysis policy;
- a compiled-DLL `extension.yaml` contract;
- WPF assemblies, `System.Windows` source, and `.xaml` markup that must move to Avalonia;
- renamed asynchronous dialog, main-view, game-operation, and web-view calls.

It deliberately does not rewrite source or project files. A static result also cannot prove provider behavior, event ordering, native view lifetime, or runtime compatibility.

## Project baseline

Use the Toolbox SDK 7 templates as the project reference contract:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <NuGetAudit>true</NuGetAudit>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="PlayniteSDK" Version="7.0.0">
    <IncludeAssets>compile;build;buildTransitive;analyzers</IncludeAssets>
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
  <PackageReference Include="Avalonia" Version="12.1.0">
    <IncludeAssets>compile;build;buildTransitive;analyzers</IncludeAssets>
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
</ItemGroup>
```

Playnite supplies both assemblies. A plugin package must not carry its own `Playnite.SDK.dll` or Avalonia runtime assemblies into the isolated host.

## Common API moves

| SDK 6 / WPF | SDK 7 / Avalonia |
|---|---|
| `System.Windows.Controls.Control` | `Avalonia.Controls.Control` |
| `.xaml` presentation markup | `.axaml` with `xmlns="https://github.com/avaloniaui"` |
| WPF `Window`, `Dock`, brushes, key gestures, dispatcher | Avalonia equivalents |
| `Dialogs.ShowMessage(...)` and file/folder selectors | corresponding `...Async(...)` call, awaited |
| `MainView.OpenPluginSettings(...)` | `OpenPluginSettingsAsync(...)` |
| `MainView.OpenEditDialog(...)` | `OpenEditDialogAsync(...)` |
| `StartGame`, `InstallGame`, `UninstallGame` | corresponding `...Async` operation |
| `IWebView.NavigateAndWait` | `NavigateAsync` |
| synchronous page/cookie methods | `GetPageTextAsync`, `GetPageSourceAsync`, and async cookie methods |
| `GetCurrentAddress()` | `Address` |
| width/height web-view factory overloads | `WebViewSettings` |

## Web-view backends

SDK 7 web views run on each operating system's native engine through Avalonia's WebView: WebView2 (Edge/Chromium) on Windows, WKWebView on macOS, and WebKit (WPE/WebKitGTK) on Linux. Playnite does not bundle a browser engine; engine security updates arrive with the operating system or distribution. This is the final architecture, not a temporary bridge — do not take a dependency on Chromium-specific behavior.

Capability differences between engines surface as explicit compatibility errors instead of silently changed behavior. Currently rejected everywhere: per-view JavaScript disabling; completed response metadata/body capture (`ResourceLoaded`); non-default SameSite/Priority cookie writes. On Linux, cookie management runs through Playnite's own WebKitGTK bridge; if the active adapter cannot expose the native web view, cookie APIs throw `PlatformNotSupportedException` rather than pretending to succeed. Write plugins so that cookie-dependent flows fail visibly and recover, and do not remove those errors or silently change the requested policy.

## Platform support

SDK 7 plugins are cross-platform: the same package loads on Windows and Linux, and platform-specific behavior belongs behind `OperatingSystem` checks inside the plugin. SDK 6 plugins load only on Windows through the WPF compatibility layer; on Linux the host records a precise per-plugin incompatibility message instead of loading them, and the add-on store lists them as incompatible with the reason shown. Ship SDK 7 packages if you want your add-on available on Linux and Steam Deck.

## Required completion gate

After resolving every error:

1. Restore and build Release with `TreatWarningsAsErrors=true` and `NuGetAudit=true`.
2. Confirm the output contains the plugin DLL and manifest, but no host SDK/Avalonia DLLs.
3. Load the plugin in Avalonia Playnite and exercise every advertised settings, menu, custom UI, controller, metadata/library, and web-view surface.
4. Keep the SDK 6 release available until the SDK 7 build reaches behavioral parity.
