using Xunit;

public class CellTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 3)]
    [InlineData("日本語", 6)]            // East Asian wide
    [InlineData("é", 1)]          // combining accent takes no column
    [InlineData("\U0001F600", 2)]       // emoji
    [InlineData("", 1)]           // Nerd Font icon (private use area)
    public void TextWidth_Of(string text, int expected) => Assert.Equal(expected, TextWidth.Of(text));

    [Fact]
    public void ConcatenationAddsWidths()
    {
        var cell = Cell.Plain("ab") + Cell.Space(2) + Cell.Plain("日");
        Assert.Equal("ab  日", cell.Text);
        Assert.Equal(6, cell.Width);
    }

    [Fact]
    public void PaddingNeverTruncates()
    {
        Assert.Equal("  ab", Cell.Plain("ab").PadLeft(4).Text);
        Assert.Equal("ab  ", Cell.Plain("ab").PadRight(4).Text);
        Assert.Equal("abcdef", Cell.Plain("abcdef").PadLeft(4).Text);
    }

    [Fact]
    public void PaddingCountsColumnsNotCharacters() => Assert.Equal("日本  ", Cell.Plain("日本").PadRight(6).Text);

    [Fact]
    public void PaintWithoutColourIsPlain() =>
        Assert.Equal(new Cell("hi", 2), Make.Plain().Paint(Theme.Dir, "hi"));

    [Fact]
    public void PaintTrueColour()
    {
        var cell = new Terminal(Piped: false, Color: true, TrueColor: true, Width: 80).Paint(new Rgb(1, 2, 3), "hi");
        Assert.Equal("\e[38;2;1;2;3mhi\e[0m", cell.Text);
        Assert.Equal(2, cell.Width);   // escape codes take no columns
    }

    [Fact]
    public void PaintFallsBackToThe256ColourCube()
    {
        var term = new Terminal(Piped: false, Color: true, TrueColor: false, Width: 80);
        Assert.Equal("\e[38;5;196mx\e[0m", term.Paint(new Rgb(255, 0, 0), "x").Text);
        Assert.Equal("\e[38;5;16mx\e[0m", term.Paint(new Rgb(0, 0, 0), "x").Text);
    }

    [Fact]
    public void LinkKeepsWidthAndEscapesThePath()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "POSIX path");
        var cell = Terminal.Link(Cell.Plain("a b"), "/tmp/a b");
        Assert.Equal("\e]8;;file:///tmp/a%20b\e\\a b\e]8;;\e\\", cell.Text);
        Assert.Equal(3, cell.Width);
    }
}
