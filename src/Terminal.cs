/// <summary>What the output device can do, worked out once from the environment.</summary>
sealed record Terminal(bool Piped, bool Color, bool TrueColor, int Width)
{
    public static Terminal Detect(ColorWhen when)
    {
        Func<string, string?> env = Environment.GetEnvironmentVariable;
        bool piped = Console.IsOutputRedirected;
        bool color = when switch
        {
            ColorWhen.Always => true,
            ColorWhen.Never => false,
            _ => env("CLICOLOR_FORCE") is { Length: > 0 } and not "0"
                 || !piped && string.IsNullOrEmpty(env("NO_COLOR")) && env("TERM") != "dumb",
        };
        return new(piped, color, env("COLORTERM") is "truecolor" or "24bit", DetectWidth(piped));
    }

    static int DetectWidth(bool piped)
    {
        try { if (!piped && Console.WindowWidth > 0) return Console.WindowWidth; } catch (IOException) { }
        return int.TryParse(Environment.GetEnvironmentVariable("COLUMNS"), out var columns) && columns > 0 ? columns : 80;
    }

    public Cell Paint(Rgb c, string s)
    {
        if (!Color || s.Length == 0) return Cell.Plain(s);
        static int Cube(byte v) => (int)Math.Round(v / 255.0 * 5);   // 256-colour fallback: the 6x6x6 cube
        var code = TrueColor ? $"38;2;{c.R};{c.G};{c.B}" : $"38;5;{16 + 36 * Cube(c.R) + 6 * Cube(c.G) + Cube(c.B)}";
        return new($"\e[{code}m{s}\e[0m", TextWidth.Of(s));
    }

    /// <summary>Wraps a cell in an OSC 8 hyperlink to a local file.</summary>
    public static Cell Link(Cell cell, string fullPath) =>
        cell with { Text = $"\e]8;;{new Uri(fullPath).AbsoluteUri}\e\\{cell.Text}\e]8;;\e\\" };
}
