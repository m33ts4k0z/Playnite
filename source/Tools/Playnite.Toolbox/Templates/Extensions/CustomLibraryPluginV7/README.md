# _name_

This library integration targets Playnite SDK 7, .NET 10, and Avalonia 12. The Playnite host supplies the SDK and Avalonia runtime assemblies; keep the compile-only package settings in the project file when adding host-shared packages.

Implement `GetGames` with stable provider game IDs and honor the supplied cancellation token. Build with `dotnet build -c Release`, then package the output with Playnite Toolbox v3.
