#!/usr/bin/env -S dotnet --
#:sdk Cake.Sdk@7.0.0

// Build script for colorcls (Cake, "C# make").
//
//   dotnet cake.cs                       run the tests, then publish the native AOT binary into ./artifacts/colorcls
//   dotnet cake.cs --target=Test         run the unit tests only
//   dotnet cake.cs --target=Install      publish, then symlink it into a bin folder on your PATH
//   dotnet cake.cs --target=Uninstall    remove that symlink
//   dotnet cake.cs --target=Clean        delete ./artifacts
//
//   --bindir=/some/bin   install somewhere specific (default: first writable folder on PATH out of
//                        /usr/local/bin, ~/.local/bin, $HOMEBREW_PREFIX/bin)
//   --name=colorcls      name of the installed command

var target = Argument("target", "Default");
var name = Argument("name", "colorcls");
var exe = OperatingSystem.IsWindows() ? ".exe" : "";

var root = Context.Environment.WorkingDirectory;
var binary = root.Combine("artifacts").Combine("colorcls").CombineWithFilePath("colorcls" + exe).FullPath;

string BinDir()
{
    if (HasArgument("bindir")) return System.IO.Path.GetFullPath(Argument<string>("bindir"));

    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var path = (EnvironmentVariable("PATH") ?? "").Split(System.IO.Path.PathSeparator);
    string?[] candidates =
    [
        "/usr/local/bin",
        System.IO.Path.Combine(home, ".local", "bin"),
        EnvironmentVariable("HOMEBREW_PREFIX") is { Length: > 0 } brew ? brew + "/bin" : null,
        "/opt/homebrew/bin",
    ];
    foreach (var dir in candidates)
        if (dir is not null && path.Contains(dir) && Writable(dir)) return dir;

    throw new CakeException(
        "No writable bin folder found on PATH. Pass one explicitly, e.g. --bindir=$HOME/.local/bin");
}

static bool Writable(string dir)
{
    if (!System.IO.Directory.Exists(dir)) return false;
    var probe = System.IO.Path.Combine(dir, $".colorcls-probe-{Environment.ProcessId}");
    try { System.IO.File.WriteAllBytes(probe, []); System.IO.File.Delete(probe); return true; }
    catch (Exception e) when (e is UnauthorizedAccessException or System.IO.IOException) { return false; }
}

// True for a symlink, including a dangling one (File.Exists is false for those).
static bool IsSymlink(string path) => new System.IO.FileInfo(path).LinkTarget is not null;

Task("Clean")
    .Does(() => CleanDirectory("./artifacts"));

Task("Test")
    .Description("Run the unit tests (tests/tests.cs, an xunit file-based app)")
    .Does(() =>
    {
        var exitCode = StartProcess("dotnet", new ProcessSettings { Arguments = "tests/tests.cs" });
        if (exitCode != 0) throw new CakeException("Tests failed");
    });

Task("Publish")
    .Description("Compile colorcls.cs to a native AOT binary")
    .IsDependentOn("Test")
    .Does(() =>
    {
        var settings = new DotNetPublishSettings { Configuration = "Release" };

        // Homebrew's source-built .NET links AOT binaries against Homebrew's OpenSSL, brotli and
        // zstd, which the system linker does not look for unless told where they live.
        var brewLib = (EnvironmentVariable("HOMEBREW_PREFIX") ?? "/opt/homebrew") + "/lib";
        if (OperatingSystem.IsMacOS() && System.IO.Directory.Exists(brewLib))
        {
            var existing = EnvironmentVariable("LIBRARY_PATH");
            settings.EnvironmentVariables = new Dictionary<string, string?>
            {
                ["LIBRARY_PATH"] = string.IsNullOrEmpty(existing) ? brewLib : $"{brewLib}:{existing}",
            };
        }

        DotNetPublish("colorcls.cs", settings);
        if (!FileExists(binary)) throw new CakeException($"Expected {binary} after publish");
        Information("Built {0}", binary);
    });

Task("Install")
    .Description("Symlink the binary into a bin folder on PATH")
    .IsDependentOn("Publish")
    .Does(() =>
    {
        var link = System.IO.Path.Combine(BinDir(), name + exe);
        if (IsSymlink(link)) System.IO.File.Delete(link);
        else if (System.IO.File.Exists(link) || System.IO.Directory.Exists(link))
            throw new CakeException($"{link} exists and is not a symlink; refusing to overwrite it");

        System.IO.File.CreateSymbolicLink(link, binary);
        Information("Installed {0} -> {1}", link, binary);
    });

Task("Uninstall")
    .Does(() =>
    {
        var link = System.IO.Path.Combine(BinDir(), name + exe);
        if (!IsSymlink(link)) { Information("Nothing to remove at {0}", link); return; }
        if (new System.IO.FileInfo(link).LinkTarget != binary)
            throw new CakeException($"{link} points somewhere else; leaving it alone");
        System.IO.File.Delete(link);
        Information("Removed {0}", link);
    });

Task("Default").IsDependentOn("Publish");

RunTarget(target);
