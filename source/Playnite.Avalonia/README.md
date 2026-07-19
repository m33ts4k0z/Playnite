# Playnite Avalonia foundation

This project is the cross-platform UI foundation for Phase 3 of the Avalonia port.
It targets plain `net10.0` and intentionally has no dependency on WPF, Win32, or
`Playnite.Core`, so it remains a reusable UI layer for either application shell.

The foundation currently contains:

- transactional loading of loose runtime XAML themes and localization dictionaries;
- a lookless `GamePanel` control with a theme-owned `PART_*` contract;
- a recycling `UniformGridVirtualizingPanel` for large game libraries;
- a cross-platform gamepad input bridge using routed Avalonia key events and
  explicit commands.

`Playnite.Avalonia.ControlsGallery` is a code-only executable that exercises the
foundation without compiled XAML. Run its automated checks with:

```powershell
dotnet run --project source\Playnite.Avalonia.ControlsGallery\Playnite.Avalonia.ControlsGallery.csproj -c Release -- --auto
```

The gallery exits non-zero if any foundation check fails and writes
`controls-gallery-results.txt` beside its output assembly.
