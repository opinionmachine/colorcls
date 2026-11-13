using Xunit;

public class RendererTests
{
    static string Render(Layout layout, List<Entry> entries, int width = 80, int depth = int.MaxValue,
        Func<string, List<Entry>?>? children = null)
    {
        var output = new StringWriter { NewLine = "\n" };
        var term = Make.Plain(width);
        new Renderer(output, new Columns(term, Make.Options("--no-icons"), Make.Now), term)
            .Render(layout, entries, depth, children ?? (_ => null));
        return output.ToString();
    }

    static List<Entry> Files(params string[] names) => names.Select(n => Make.File(n)).ToList();

    [Fact]
    public void NothingToShowPrintsNothing() => Assert.Equal("", Render(Layout.Grid, []));

    [Fact]
    public void GridFillsColumnsTopToBottomAndPacksAsManyAsFit()
    {
        // widths 2, 3, 1 plus two gaps of 2 is exactly 10 columns
        Assert.Equal("""
            a   ccc  e
            bb  dd

            """, Render(Layout.Grid, Files("a", "bb", "ccc", "dd", "e"), width: 10));
    }

    [Fact]
    public void GridDropsAColumnWhenOneCharacterTooNarrow()
    {
        Assert.Equal("""
            a    dd
            bb   e
            ccc

            """, Render(Layout.Grid, Files("a", "bb", "ccc", "dd", "e"), width: 9));
    }

    [Fact]
    public void GridFallsBackToOneColumn() =>
        Assert.Equal("alpha\nbeta\n", Render(Layout.Grid, Files("alpha", "beta"), width: 4));

    [Fact]
    public void GridOnOneRowHasNoTrailingSpaces() =>
        Assert.Equal("a  b  c\n", Render(Layout.Grid, Files("a", "b", "c")));

    [Fact]
    public void GridAlignsWideCharactersByColumns()
    {
        Assert.Equal("""
            日本  c
            ab

            """, Render(Layout.Grid, Files("日本", "ab", "c"), width: 8));
    }

    [Fact]
    public void GitColumnAppearsOnlyInsideAWorkTree()
    {
        Assert.Equal("a\nb\n", Render(Layout.OnePerLine, Files("a", "b")));

        List<Entry> tracked = [Make.File("a", git: GitMarks.Clean), Make.File("b", git: GitMarks.Added | GitMarks.Modified), Make.Dir(".git")];
        Assert.Equal("""
              ✓ a
             AM b
                .git/

            """, Render(Layout.OnePerLine, tracked));
    }

    [Fact]
    public void GitColumnWidensForAnEntryWithManyMarks()
    {
        var four = GitMarks.Untracked | GitMarks.Added | GitMarks.Deleted | GitMarks.Modified;
        Assert.Equal("""
            ?ADM ./
               ✓ a

            """, Render(Layout.OnePerLine, [Make.Dir(".", git: four), Make.File("a", git: GitMarks.Clean)]));
    }

    [Fact]
    public void LongListing()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "mode and ownership columns are POSIX only");
        List<Entry> entries =
        [
            Make.Dir("src", git: GitMarks.Modified),
            Make.File("notes.txt", size: 1536, git: GitMarks.Clean),
            Make.File("latest", kind: EntryKind.Symlink, target: "notes.txt", mode: (UnixFileMode)0b111_111_111, size: 9, git: GitMarks.Untracked),
        ];
        Assert.Equal("""
            drwxr-xr-x  -  -           -  15 Jan 2024 10:30    M  src/
            -rw-r--r--  -  -     1.5 KiB  15 Jan 2024 10:30    ✓  notes.txt
            lrwxrwxrwx  -  -       9 B    15 Jan 2024 10:30    ?  latest ⇒ notes.txt

            """, Render(Layout.Long, entries));
    }

    [Fact]
    public void LongListingOutsideAWorkTreeHasNoGitColumn()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "mode and ownership columns are POSIX only");
        Assert.Equal("-rw-r--r--  -  -       0 B    15 Jan 2024 10:30  a\n", Render(Layout.Long, Files("a")));
    }

    [Fact]
    public void TreeDrawsBranchesAndRecursesIntoDirectories()
    {
        var inside = new Dictionary<string, List<Entry>>
        {
            ["/x/src"] = [Make.Dir("deep"), Make.File("a.cs")],
            ["/x/deep"] = Files("x.py"),
        };
        Assert.Equal("""
             ├── src/
             │    ├── deep/
             │    │    └── x.py
             │    └── a.cs
             └── z

            """, Render(Layout.Tree, [Make.Dir("src"), Make.File("z")], children: inside.GetValueOrDefault));
    }

    [Fact]
    public void TreeStopsAtTheRequestedDepth()
    {
        var asked = new List<string>();
        var text = Render(Layout.Tree, [Make.Dir("src")], depth: 1, children: path => { asked.Add(path); return Files("a"); });
        Assert.Equal(" └── src/\n", text);
        Assert.Empty(asked);
    }

    [Fact]
    public void TreeSkipsUnreadableAndEmptyDirectories() =>
        Assert.Equal(" ├── a/\n └── b/\n", Render(Layout.Tree, [Make.Dir("a"), Make.Dir("b")],
            children: path => path.EndsWith("a") ? null : []));
}
