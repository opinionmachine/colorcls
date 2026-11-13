#!/usr/bin/env -S dotnet --
// Unit tests for colorcls: a second file-based app that includes the same sources.
//
//   Run:  ./tests/tests.cs        (or: dotnet cake.cs --target=Test)

#:package xunit.v3@4.0.3
#:property TargetFramework=net11.0
#:property LangVersion=15
#:property PublishAot=false
#:property PackAsTool=false
#:include ../src/Options.cs
#:include ../src/Terminal.cs
#:include ../src/Listing.cs
#:include ../src/Entry.cs
#:include ../src/Posix.cs
#:include ../src/GitRepo.cs
#:include ../src/Icons.cs
#:include ../src/Theme.cs
#:include ../src/Cell.cs
#:include ../src/Columns.cs
#:include ../src/Renderer.cs
#:include OptionsTests.cs
#:include CellTests.cs
#:include GitRepoTests.cs
#:include ListingTests.cs
#:include ColumnsTests.cs
#:include RendererTests.cs

/// <summary>Builders shared by the test classes.</summary>
static class Make
{
    public static readonly DateTime Now = new(2024, 6, 1, 12, 0, 0);

    public static Entry File(string name, long size = 0, DateTime? mtime = null, GitMarks? git = null,
        UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead,
        EntryKind kind = EntryKind.File, string? target = null, bool dead = false) => new()
    {
        Name = name, Display = name, FullPath = "/x/" + name, Kind = kind, Size = size,
        MTime = mtime ?? new DateTime(2024, 1, 15, 10, 30, 0), Git = git, Mode = mode, LinkTarget = target, IsDead = dead,
    };

    public static Entry Dir(string name, GitMarks? git = null) =>
        File(name, git: git, kind: EntryKind.Directory, mode: (UnixFileMode)0b111_101_101);

    /// <summary>A terminal without colour, so output can be compared as plain text.</summary>
    public static Terminal Plain(int width = 80) => new(Piped: false, Color: false, TrueColor: false, Width: width);

    public static Options Options(params string[] args) => global::Options.Parse(args);

    /// <summary>A scratch directory that is removed when disposed.</summary>
    public sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("colorcls-test-").FullName;
        public string At(string relative) => System.IO.Path.Combine(Path, relative);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
