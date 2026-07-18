# _name_

This project targets Playnite SDK 7, .NET 10, and Avalonia 12. The Playnite host supplies the SDK and Avalonia runtime assemblies; keep the compile-only package settings in the project file when adding host-shared packages.

Build with `dotnet build -c Release`. Copy the Release output to a Playnite extensions directory while developing, or package the directory with Playnite Toolbox v3.
