#!/usr/bin/env -S dotnet --
// colorcls - a file-based C# take on colorls (https://github.com/athityakumar/colorls):
// a colourised ls with icons and git status. All credit for the idea and the look goes there.
//
//   Run:      ./colorcls.cs -la          (or: dotnet colorcls.cs -la)
//   Install:  dotnet cake.cs --target=Install   (native AOT binary, symlinked onto your PATH)
//
// Icons need a Nerd Font in your terminal; pass --no-icons otherwise.
// Includes are listed one by one on purpose: a glob would switch off the build cache.

#:property TargetFramework=net11.0
#:property LangVersion=15
#:property PackAsTool=false
#:include src/App.cs
#:include src/Options.cs
#:include src/Terminal.cs
#:include src/Listing.cs
#:include src/Entry.cs
#:include src/Posix.cs
#:include src/GitRepo.cs
#:include src/Icons.cs
#:include src/Theme.cs
#:include src/Cell.cs
#:include src/Columns.cs
#:include src/Renderer.cs

return App.Run(args);
