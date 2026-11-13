using Xunit;

public class ListingTests
{
    static readonly DateTime day = new(2024, 1, 1);

    static string[] Names(IEnumerable<Entry> entries) => entries.Select(e => e.Display).ToArray();

    [Fact]
    public void SortsByNameIgnoringCaseAndLeadingDots()
    {
        Entry[] entries = [Make.File("zeta"), Make.File(".config"), Make.File("Beta"), Make.File("alpha")];
        Assert.Equal(["alpha", "Beta", ".config", "zeta"], Names(Listing.Sort(entries, Make.Options())));
    }

    [Fact]
    public void SortsNewestFirst()
    {
        Entry[] entries = [Make.File("old", mtime: day), Make.File("new", mtime: day.AddDays(2)), Make.File("mid", mtime: day.AddDays(1))];
        Assert.Equal(["new", "mid", "old"], Names(Listing.Sort(entries, Make.Options("-t"))));
    }

    [Fact]
    public void SortsLargestFirstWithNameAsTieBreak()
    {
        Entry[] entries = [Make.File("b", size: 5), Make.File("big", size: 900), Make.File("a", size: 5)];
        Assert.Equal(["big", "a", "b"], Names(Listing.Sort(entries, Make.Options("-S"))));
    }

    [Fact]
    public void SortsByExtensionThenName()
    {
        Entry[] entries = [Make.File("b.txt"), Make.File("a.md"), Make.File("a.txt"), Make.File("noext")];
        Assert.Equal(["noext", "a.md", "a.txt", "b.txt"], Names(Listing.Sort(entries, Make.Options("-X"))));
    }

    [Fact]
    public void UnsortedKeepsDirectoryOrder()
    {
        Entry[] entries = [Make.File("c"), Make.File("a"), Make.File("b")];
        Assert.Equal(["c", "a", "b"], Names(Listing.Sort(entries, Make.Options("-U"))));
    }

    [Fact]
    public void Reverse()
    {
        Entry[] entries = [Make.File("a"), Make.File("c"), Make.File("b")];
        Assert.Equal(["c", "b", "a"], Names(Listing.Sort(entries, Make.Options("-r"))));
    }

    [Fact]
    public void GroupingKeepsTheSortOrderInsideEachGroup()
    {
        Entry[] entries = [Make.File("b"), Make.Dir("z"), Make.File("a"), Make.Dir("y")];
        Assert.Equal(["y", "z", "a", "b"], Names(Listing.Sort(entries, Make.Options("--sd"))));
        Assert.Equal(["a", "b", "y", "z"], Names(Listing.Sort(entries, Make.Options("--sf"))));
        Assert.Equal(["z", "y", "b", "a"], Names(Listing.Sort(entries, Make.Options("--sd", "-r"))));
    }

    [Fact]
    public void Filter()
    {
        Entry[] entries = [Make.File("a"), Make.File(".hidden"), Make.Dir("d"), Make.Dir(".git")];
        Assert.Equal(["a", "d"], Names(Listing.Filter(entries, Make.Options())));
        Assert.Equal(["a", ".hidden", "d", ".git"], Names(Listing.Filter(entries, Make.Options("-A"))));
        Assert.Equal(["d"], Names(Listing.Filter(entries, Make.Options("-d"))));
        Assert.Equal(["a", ".hidden"], Names(Listing.Filter(entries, Make.Options("-f", "-a"))));
    }

    [Fact]
    public void ReadsADirectory()
    {
        using var tmp = new Make.TempDir();
        File.WriteAllText(tmp.At("file.txt"), "12345");
        Directory.CreateDirectory(tmp.At("sub"));
        File.CreateSymbolicLink(tmp.At("good"), "file.txt");
        File.CreateSymbolicLink(tmp.At("broken"), "nowhere");

        var entries = Listing.ReadDirectory(tmp.Path, Make.Options("--no-git"), withDots: false).ToDictionary(e => e.Name);

        Assert.Equal(["broken", "file.txt", "good", "sub"], entries.Keys.Order());
        Assert.Equal((EntryKind.File, 5L), (entries["file.txt"].Kind, entries["file.txt"].Size));
        Assert.True(entries["sub"].IsDir);
        Assert.Equal((EntryKind.Symlink, "file.txt", false), (entries["good"].Kind, entries["good"].LinkTarget, entries["good"].IsDead));
        Assert.True(entries["broken"].IsDead);
        Assert.All(entries.Values, e => Assert.Null(e.Git));
    }

    [Fact]
    public void WithDotsAddsTheDirectoryAndItsParent()
    {
        using var tmp = new Make.TempDir();
        var entries = Listing.ReadDirectory(tmp.Path, Make.Options("--no-git"), withDots: true);
        Assert.Equal([".", ".."], Names(entries));
        Assert.All(entries, e => Assert.True(e.IsDir));
        Assert.Equal(Path.GetDirectoryName(tmp.Path), entries[1].FullPath);
    }

    [Fact]
    public void AnExecutableFileIsExecutableButADirectoryIsNot()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "POSIX permissions");
        using var tmp = new Make.TempDir();
        File.WriteAllText(tmp.At("run.sh"), "");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(tmp.At("run.sh"), (UnixFileMode)0b111_101_101);
        Directory.CreateDirectory(tmp.At("dir"));

        var entries = Listing.ReadDirectory(tmp.Path, Make.Options("--no-git"), withDots: false).ToDictionary(e => e.Name);

        Assert.True(entries["run.sh"].IsExec);
        Assert.Equal((UnixFileMode)0b111_101_101, entries["run.sh"].Mode);
        Assert.False(entries["dir"].IsExec);
        Assert.NotEqual("-", entries["run.sh"].Owner);   // lstat supplied a uid and it resolved to something
    }

    [Fact]
    public void ACommandLineFileIsShownAsTypedButLookedUpByName()
    {
        using var tmp = new Make.TempDir();
        File.WriteAllText(tmp.At("Dockerfile"), "");

        var entry = Listing.ReadArgument(tmp.At("Dockerfile"), Make.Options("--no-git"));

        Assert.NotNull(entry);
        Assert.Equal(tmp.At("Dockerfile"), entry.Display);
        Assert.Equal("Dockerfile", entry.Name);
        Assert.True(entry.Icon.Recognised);
    }

    [Fact]
    public void AMissingCommandLineFileIsNull()
    {
        using var tmp = new Make.TempDir();
        Assert.Null(Listing.ReadArgument(tmp.At("nope"), Make.Options()));
    }

    [Fact]
    public void ADanglingLinkOnTheCommandLineStillExists()
    {
        using var tmp = new Make.TempDir();
        File.CreateSymbolicLink(tmp.At("broken"), "nowhere");
        Assert.True(Listing.ReadArgument(tmp.At("broken"), Make.Options("--no-git"))?.IsDead);
    }
}
