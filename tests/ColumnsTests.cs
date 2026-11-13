using Xunit;

public class ColumnsTests
{
    static readonly Columns columns = new(Make.Plain(), Make.Options(), Make.Now);
    static readonly Columns bare = new(Make.Plain(), Make.Options("--no-icons"), Make.Now);

    [Theory]
    [InlineData(EntryKind.File, 0b110_100_100, "-rw-r--r--")]
    [InlineData(EntryKind.Directory, 0b111_101_101, "drwxr-xr-x")]
    [InlineData(EntryKind.Symlink, 0b111_111_111, "lrwxrwxrwx")]
    [InlineData(EntryKind.Fifo, 0b110_000_000, "prw-------")]
    [InlineData(EntryKind.Socket, 0, "s---------")]
    [InlineData(EntryKind.CharDevice, 0b110_110_110, "crw-rw-rw-")]
    [InlineData(EntryKind.BlockDevice, 0b110_110_000, "brw-rw----")]
    public void Mode(EntryKind kind, int bits, string expected) =>
        Assert.Equal(expected, columns.Mode(Make.File("f", kind: kind, mode: (UnixFileMode)bits)).Text);

    [Theory]
    [InlineData(0b100_111_101_101, "-rwsr-xr-x")]   // setuid with execute
    [InlineData(0b100_110_100_100, "-rwSr--r--")]   // setuid without execute
    [InlineData(0b010_111_101_101, "-rwxr-sr-x")]   // setgid
    [InlineData(0b001_111_111_111, "-rwxrwxrwt")]   // sticky with execute
    [InlineData(0b001_111_111_110, "-rwxrwxrwT")]   // sticky without execute
    public void SpecialBitsShareTheExecuteSlots(int bits, string expected) =>
        Assert.Equal(expected, columns.Mode(Make.File("f", mode: (UnixFileMode)bits)).Text);

    [Theory]
    [InlineData(0L, "     0 B  ")]
    [InlineData(1023L, "  1023 B  ")]
    [InlineData(1024L, "   1.0 KiB")]
    [InlineData(1536L, "   1.5 KiB")]
    [InlineData(3_000_000L, "   2.9 MiB")]
    [InlineData(5L << 30, "   5.0 GiB")]
    public void Size(long bytes, string expected) => Assert.Equal(expected, columns.Size(Make.File("f", size: bytes)).Text);

    [Fact]
    public void ADirectoryHasNoSize() => Assert.Equal("         -", columns.Size(Make.Dir("d")).Text);

    [Fact]
    public void TimeIsFixedWidthAndLocaleIndependent() =>
        Assert.Equal("05 Mar 2024 09:07", columns.Time(Make.File("f", mtime: new DateTime(2024, 3, 5, 9, 7, 0))).Text);

    [Fact]
    public void TimeColourFollowsAge()
    {
        var colour = new Columns(new Terminal(false, Color: true, TrueColor: true, 80), Make.Options(), Make.Now);
        string Code(TimeSpan age) => colour.Time(Make.File("f", mtime: Make.Now - age)).Text.Split('m')[0];
        Assert.Equal(Code(TimeSpan.FromMinutes(5)), Code(TimeSpan.FromMinutes(59)));
        Assert.NotEqual(Code(TimeSpan.FromMinutes(59)), Code(TimeSpan.FromHours(2)));
        Assert.NotEqual(Code(TimeSpan.FromHours(2)), Code(TimeSpan.FromDays(2)));
    }

    [Theory]
    [InlineData(null, "   ")]
    [InlineData(GitMarks.Clean, "  ✓")]
    [InlineData(GitMarks.Modified, "  M")]
    [InlineData(GitMarks.Modified | GitMarks.Added, " AM")]
    [InlineData(GitMarks.Untracked | GitMarks.Ignored | GitMarks.Conflict, "!?U")]
    public void GitCellIsRightAlignedInDisplayOrder(GitMarks? marks, string expected) =>
        Assert.Equal(new Cell(expected, 3), columns.Git(marks, 3));

    [Fact]
    public void GitColumnWidth()
    {
        Assert.Equal(0, Columns.GitWidth([Make.File("a"), Make.File("b")]));                       // nothing in a work tree
        Assert.Equal(3, Columns.GitWidth([Make.File("a", git: GitMarks.Clean), Make.File("b")]));   // the minimum
        var four = GitMarks.Untracked | GitMarks.Added | GitMarks.Deleted | GitMarks.Modified;
        Assert.Equal(4, Columns.GitWidth([Make.File("a", git: four), Make.File("b", git: GitMarks.Clean)]));
    }

    [Fact]
    public void LabelMarksDirectoriesAndPutsTheIconFirst()
    {
        Assert.Equal("src/", bare.Label(Make.Dir("src")).Text);
        Assert.Equal("a.txt", bare.Label(Make.File("a.txt")).Text);
        var withIcon = columns.Label(Make.File("a.txt"));
        Assert.EndsWith("  a.txt", withIcon.Text);
        Assert.Equal(1 + 2 + 5, withIcon.Width);
    }

    [Fact]
    public void LabelCanBeAHyperlink()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "POSIX path");
        var linked = new Columns(Make.Plain(), Make.Options("--no-icons", "--hyperlink"), Make.Now).Label(Make.File("a.txt"));
        Assert.Equal("\e]8;;file:///x/a.txt\e\\a.txt\e]8;;\e\\", linked.Text);
        Assert.Equal(5, linked.Width);
    }

    [Fact]
    public void LinkTarget()
    {
        Assert.Equal("", columns.LinkTarget(Make.File("plain")).Text);
        Assert.Equal(" ⇒ README.md", columns.LinkTarget(Make.File("l", kind: EntryKind.Symlink, target: "README.md")).Text);
        Assert.Equal(" ⇒ gone [dead link]", columns.LinkTarget(Make.File("l", kind: EntryKind.Symlink, target: "gone", dead: true)).Text);
    }

    [Theory]
    [InlineData("Program.cs", true)]
    [InlineData("Dockerfile", true)]
    [InlineData("README.MD", true)]        // case-insensitive
    [InlineData("data.xyzzy", false)]
    [InlineData("noextension", false)]
    public void IconRecognition(string name, bool recognised) => Assert.Equal(recognised, Make.File(name).Icon.Recognised);

    [Fact]
    public void FoldersHaveTheirOwnIcons() =>
        Assert.NotEqual(Make.Dir("plain").Icon.Glyph, Make.Dir(".git").Icon.Glyph);
}
