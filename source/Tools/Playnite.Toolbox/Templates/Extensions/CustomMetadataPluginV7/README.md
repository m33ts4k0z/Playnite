# _name_

This metadata provider targets Playnite SDK 7, .NET 10, and Avalonia 12. The Playnite host supplies the SDK and Avalonia runtime assemblies; keep the compile-only package settings in the project file when adding host-shared packages.

Declare supported fields on the plugin and available fields on each provider instance. Honor cancellation before network or file work. Build with `dotnet build -c Release`, then package the output with Playnite Toolbox v3.
