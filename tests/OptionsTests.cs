using System.Text.RegularExpressions;
using Xunit;

public class OptionsTests
{
    [Fact]
    public void Defaults()
    {
        var o = Options.Parse([]);
        Assert.Equal(Layout.Grid, o.Layout);
        Assert.Equal(Hidden.Skip, o.Hidden);
        Assert.Equal(SortKey.Name, o.Sort);
        Assert.Equal(ColorWhen.Auto, o.Color);
        Assert.True(o.Icons);
        Assert.True(o.Git);
        Assert.Equal(["."], o.Paths);
    }

    [Fact]
    public void ShortFlagsCombine()
    {
        var o = Options.Parse(["-lar", "src"]);
        Assert.Equal(Layout.Long, o.Layout);
        Assert.Equal(Hidden.ShowWithDots, o.Hidden);
        Assert.True(o.Reverse);
        Assert.Equal(["src"], o.Paths);
    }

    [Theory]
    [InlineData("-a -A")]
    [InlineData("-A -a")]
    public void AllWinsOverAlmostAllInEitherOrder(string args) =>
        Assert.Equal(Hidden.ShowWithDots, Options.Parse(args.Split(' ')).Hidden);

    [Theory]
    [InlineData("-1", Layout.OnePerLine)]
    [InlineData("-l", Layout.Long)]
    [InlineData("-1 -l", Layout.Long)]
    [InlineData("-l --tree", Layout.Tree)]
    [InlineData("--tree=2 -1", Layout.Tree)]
    public void LayoutPriorityIsTreeThenLongThenOnePerLine(string args, Layout expected) =>
        Assert.Equal(expected, Options.Parse(args.Split(' ')).Layout);

    [Fact]
    public void TreeDepth()
    {
        Assert.Equal(int.MaxValue, Options.Parse(["--tree"]).TreeDepth);
        Assert.Equal(3, Options.Parse(["--tree=3"]).TreeDepth);
    }

    [Theory]
    [InlineData("--color", ColorWhen.Always)]
    [InlineData("--color=never", ColorWhen.Never)]
    [InlineData("--colour=auto", ColorWhen.Auto)]
    public void Color(string arg, ColorWhen expected) => Assert.Equal(expected, Options.Parse([arg]).Color);

    [Theory]
    [InlineData("-t", SortKey.Time)]
    [InlineData("-S", SortKey.Size)]
    [InlineData("-X", SortKey.Extension)]
    [InlineData("-U", SortKey.None)]
    [InlineData("--sort=size", SortKey.Size)]
    public void Sort(string arg, SortKey expected) => Assert.Equal(expected, Options.Parse([arg]).Sort);

    [Fact]
    public void GroupingAliases()
    {
        Assert.Equal(Grouping.DirsFirst, Options.Parse(["--sd"]).Group);
        Assert.Equal(Grouping.DirsFirst, Options.Parse(["--group-directories-first"]).Group);
        Assert.Equal(Grouping.FilesFirst, Options.Parse(["--sort-files"]).Group);
    }

    [Fact]
    public void DoubleDashEndsOptions() =>
        Assert.Equal(["-l", "--tree"], Options.Parse(["--", "-l", "--tree"]).Paths);

    [Fact]
    public void LoneDashIsAPath() => Assert.Equal(["-"], Options.Parse(["-"]).Paths);

    [Theory]
    [InlineData("--bogus", "unknown option '--bogus'")]
    [InlineData("-lz", "unknown option '-z'")]
    [InlineData("--sort", "option '--sort' needs a value")]
    [InlineData("--sort=shoe", "invalid --sort value 'shoe'")]
    [InlineData("--all=yes", "option '--all' takes no value")]
    [InlineData("--tree=0", "invalid --tree depth '0'")]
    [InlineData("--color=sometimes", "invalid --color value 'sometimes'")]
    public void Errors(string arg, string message) =>
        Assert.Equal(message, Assert.Throws<ArgumentException>(() => Options.Parse([arg])).Message);

    [Fact]
    public void HelpHidesAliases()
    {
        var help = Options.HelpText();
        Assert.Contains("--color[=WHEN]", help);
        Assert.DoesNotContain("--colour", help);
        Assert.DoesNotContain("--group-directories-first", help);
    }

    [Fact]
    public void EveryFlagInTheHelpTextParses()
    {
        var flagLines = Options.HelpText().Split('\n').Where(l => l.StartsWith("  ") && l.TrimStart().StartsWith('-'));
        var names = flagLines.SelectMany(l => Regex.Matches(l[..26], @"(?<![\w-])--?[A-Za-z0-9][\w-]*").Select(m => m.Value)).ToList();
        Assert.True(names.Count > 25, $"only found {names.Count} flags in the help text");
        foreach (var name in names)
        {
            var ex = Record.Exception(() => Options.Parse([name]));
            Assert.False(ex?.Message.StartsWith("unknown option") ?? false, $"{name} is in the help text but does not parse");
        }
    }
}
