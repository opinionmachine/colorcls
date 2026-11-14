using System.Globalization;

/// <summary>Turns the fields of an entry into painted cells. No layout decisions live here.</summary>
/// <param name="now">The current time in UTC.</param>
/// <param name="toLocal">Converts a UTC time to local wall-clock time for display.</param>
sealed class Columns(Terminal term, Options options, DateTime now, Func<DateTime, DateTime> toLocal)
{
    public const int MinGitWidth = 3;

    /// <summary>Width of the git column for a listing: 0 when nothing in it is inside a work tree.</summary>
    public static int GitWidth(IReadOnlyCollection<Entry> entries) =>
        entries.Any(e => e.Git is not null) ? Math.Max(MinGitWidth, entries.Max(e => e.Git?.Count() ?? 0)) : 0;

    public Cell Git(GitMarks? marks, int width)
    {
        var cell = marks switch
        {
            null => Cell.Space(0),
            GitMarks.Clean => term.Paint(Theme.Unchanged, "✓"),
            { } m => GitMark.All.Where(x => m.HasFlag(x.Mark))
                .Select(x => term.Paint(MarkColor(x.Mark), x.Letter.ToString()))
                .Aggregate((a, b) => a + b),
        };
        return cell.PadLeft(width);
    }

    static Rgb MarkColor(GitMarks mark) => mark switch
    {
        GitMarks.Modified or GitMarks.TypeChanged => Theme.Modified,
        GitMarks.Added => Theme.Added,
        GitMarks.Deleted or GitMarks.Conflict => Theme.Deleted,
        GitMarks.Renamed or GitMarks.Copied => Theme.Renamed,
        GitMarks.Untracked => Theme.Untracked,
        _ => Theme.Dim,
    };

    /// <summary>"icon  name/"</summary>
    public Cell Label(Entry e)
    {
        var (glyph, recognised) = e.Icon;
        var color = e.IsDead ? Theme.Dead : e.IsLink ? Theme.Link : e.IsDir ? Theme.Dir : e.IsExec ? Theme.Exec
            : recognised ? Theme.Recognised : Theme.Unrecognised;
        var name = term.Paint(color, e.Display + (e.IsDir ? "/" : ""));
        if (options.Hyperlink) name = Terminal.Link(name, e.FullPath);
        return options.Icons ? term.Paint(color, glyph) + Cell.Space(2) + name : name;
    }

    /// <summary>" ⇒ target" for a symlink, empty for anything else.</summary>
    public Cell LinkTarget(Entry e) =>
        !e.IsLink ? Cell.Space(0)
        : e.IsDead ? term.Paint(Theme.Dead, $" ⇒ {e.LinkTarget} [dead link]")
        : term.Paint(Theme.Link, $" ⇒ {e.LinkTarget}");

    public Cell Mode(Entry e)
    {
        var type = e.Kind switch
        {
            EntryKind.Directory => 'd', EntryKind.Symlink => 'l', EntryKind.Fifo => 'p', EntryKind.Socket => 's',
            EntryKind.CharDevice => 'c', EntryKind.BlockDevice => 'b', _ => '-',
        };
        var cell = term.Paint(type switch { 'd' => Theme.Dir, 'l' => Theme.Link, '-' => Theme.Dim, _ => Theme.Unrecognised }, type.ToString());

        for (int i = 0; i < 9; i++)
        {
            bool on = e.Mode.HasFlag((UnixFileMode)(1 << (8 - i)));   // UserRead is the top bit, OtherExecute the bottom
            char ch = on ? "rwx"[i % 3] : '-';
            // setuid, setgid and sticky share the three execute slots: lower case when execute is also set
            var (special, letter) = i switch
            {
                2 => (UnixFileMode.SetUser, 's'), 5 => (UnixFileMode.SetGroup, 's'), 8 => (UnixFileMode.StickyBit, 't'),
                _ => (UnixFileMode.None, ' '),
            };
            if (special != UnixFileMode.None && e.Mode.HasFlag(special)) ch = on ? letter : char.ToUpperInvariant(letter);
            cell += term.Paint(ch switch { 'r' => Theme.Read, 'w' => Theme.Write, '-' => Theme.NoAccess, _ => Theme.Execute }, ch.ToString());
        }
        return cell;
    }

    public Cell Owner(Entry e) => term.Paint(Theme.User, e.Owner);
    public Cell Group(Entry e) => term.Paint(Theme.Group, e.Group);

    public Cell Size(Entry e)
    {
        if (e.IsDir) return term.Paint(Theme.Dim, "-".PadLeft(10));
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB"];
        double value = e.Size;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        var number = unit == 0 ? e.Size.ToString(CultureInfo.InvariantCulture) : value.ToString("0.0", CultureInfo.InvariantCulture);
        var color = e.Size >= 100L << 20 ? Theme.Large : e.Size >= 1L << 20 ? Theme.Medium : Theme.Small;
        return term.Paint(color, $"{number,6} {units[unit],-3}");
    }

    public Cell Time(Entry e)
    {
        var age = now - e.MTime;
        var color = age < TimeSpan.FromHours(1) ? Theme.HourOld : age < TimeSpan.FromDays(1) ? Theme.DayOld : Theme.Older;
        return term.Paint(color, toLocal(e.MTime).ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture));
    }
}
