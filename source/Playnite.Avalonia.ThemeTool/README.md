# Playnite Avalonia theme tool

This cross-platform console tool scaffolds, validates, and packages loose Avalonia theme API 3 projects.

```text
Playnite.Avalonia.ThemeTool new desktop "My theme" path/to/MyTheme
Playnite.Avalonia.ThemeTool validate path/to/MyTheme desktop
Playnite.Avalonia.ThemeTool pack path/to/MyTheme path/to/packages desktop
```

`validate` checks the manifest contract, package-contained paths, file existence, XML safety, and the required `ResourceDictionary`/`Styles` roots. The target Playnite app performs the final semantic Avalonia-XAML parse against its concrete control assembly before applying a theme. `pack` validates first and writes a `.pthm` ZIP without build output, source-control metadata, linked files, or development project files.
