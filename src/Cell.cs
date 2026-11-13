using System.Globalization;
using System.Text;

/// <summary>A piece of output that knows how many terminal columns it occupies, escape codes excluded.</summary>
readonly record struct Cell(string Text, int Width)
{
    public static Cell Plain(string text) => new(text, TextWidth.Of(text));
    public static Cell Space(int count) => new(new string(' ', count), count);

    public static Cell operator +(Cell a, Cell b) => new(a.Text + b.Text, a.Width + b.Width);

    public Cell PadLeft(int width) => Width >= width ? this : Space(width - Width) + this;
    public Cell PadRight(int width) => Width >= width ? this : this + Space(width - Width);
}

static class TextWidth
{
    public static int Of(string s)
    {
        int width = 0;
        foreach (var rune in s.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format) continue;
            width += IsWide(rune.Value) ? 2 : 1;
        }
        return width;
    }

    // East Asian wide and emoji blocks; close enough to wcwidth for file names.
    static bool IsWide(int v) => v is
        >= 0x1100 and <= 0x115F or >= 0x2E80 and <= 0xA4CF or >= 0xAC00 and <= 0xD7A3 or
        >= 0xF900 and <= 0xFAFF or >= 0xFE30 and <= 0xFE4F or >= 0xFF00 and <= 0xFF60 or
        >= 0xFFE0 and <= 0xFFE6 or >= 0x1F300 and <= 0x1FAFF or >= 0x20000 and <= 0x3FFFD;
}
