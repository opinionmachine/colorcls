public enum SortKey { Name, Time, Size, Extension, None }
public enum Grouping { None, DirsFirst, FilesFirst }
public enum Layout { Grid, OnePerLine, Long, Tree }
public enum ColorWhen { Auto, Always, Never }
public enum Hidden { Skip, Show, ShowWithDots }

sealed class Options
{
    public Hidden Hidden { get; private set; }
    public bool DirsOnly { get; private set; }
    public bool FilesOnly { get; private set; }
    public bool Reverse { get; private set; }
    public bool Report { get; private set; }
    public bool Hyperlink { get; private set; }
    public bool Help { get; private set; }
    public bool Version { get; private set; }
    public bool Icons { get; private set; } = true;
    public bool Git { get; private set; } = true;
    public SortKey Sort { get; private set; }
    public Grouping Group { get; private set; }
    public ColorWhen Color { get; private set; }
    public int TreeDepth { get; private set; }
    public IReadOnlyList<string> Paths => paths;

    public Layout Layout =>
        TreeDepth > 0 ? Layout.Tree : longListing ? Layout.Long : onePerLine ? Layout.OnePerLine : Layout.Grid;

    bool longListing, onePerLine;
    readonly List<string> paths = [];

    // One row per flag: the parser and the help text are both generated from this table.
    // Spec: space-separated names; a long name may end in "=VALUE" (required) or "[=VALUE]" (optional).
    // A null help string keeps an alias out of the help text.
    sealed record Flag(string Spec, string? Help, Action<Options, string?> Apply)
    {
        public IEnumerable<string> Names => Spec.Split(' ').Select(n => n.Split('[', '=')[0]);
        public bool TakesValue => Spec.Contains('=');
        public bool NeedsValue => TakesValue && !Spec.Contains('[');
    }

    static readonly (string Title, Flag[] Flags)[] sections =
    [
        ("Display",
        [
            new("-l --long", "long listing: mode, owner, group, size, mtime, git, name", (o, _) => o.longListing = true),
            new("-1", "one entry per line", (o, _) => o.onePerLine = true),
            new("--tree[=DEPTH]", "recurse and draw a tree", (o, v) => o.TreeDepth =
                v is null ? int.MaxValue : int.TryParse(v, out var d) && d > 0 ? d : throw Invalid("--tree depth", v)),
            new("--report", "print a summary of what was found", (o, _) => o.Report = true),
            new("--no-icons", "don't print Nerd Font icons", (o, _) => o.Icons = false),
            new("--hyperlink", "make names clickable (OSC 8)", (o, _) => o.Hyperlink = true),
            new("--color[=WHEN]", "always | auto | never (default auto; honours NO_COLOR)", (o, v) => o.Color = ParseColor(v)),
            new("--colour[=WHEN]", null, (o, v) => o.Color = ParseColor(v)),
        ]),
        ("Filtering",
        [
            new("-a --all", "include entries starting with . (and . / ..)", (o, _) => o.Hidden = Hidden.ShowWithDots),
            new("-A --almost-all", "like -a but without . and ..", (o, _) => o.Hidden = (Hidden)Math.Max((int)o.Hidden, (int)Hidden.Show)),
            new("-d --dirs", "directories only", (o, _) => o.DirsOnly = true),
            new("-f --files", "files only", (o, _) => o.FilesOnly = true),
        ]),
        ("Sorting",
        [
            new("-t", "newest first", (o, _) => o.Sort = SortKey.Time),
            new("-S", "largest first", (o, _) => o.Sort = SortKey.Size),
            new("-X", "by extension", (o, _) => o.Sort = SortKey.Extension),
            new("-U", "directory order (unsorted)", (o, _) => o.Sort = SortKey.None),
            new("--sort=KEY", "name | time | size | extension | none", (o, v) => o.Sort = v switch
            {
                "name" => SortKey.Name, "time" => SortKey.Time, "size" => SortKey.Size,
                "extension" => SortKey.Extension, "none" => SortKey.None,
                _ => throw Invalid("--sort value", v),
            }),
            new("-r --reverse", "reverse the order", (o, _) => o.Reverse = true),
            new("--sd --sort-dirs", "directories first", (o, _) => o.Group = Grouping.DirsFirst),
            new("--group-directories-first", null, (o, _) => o.Group = Grouping.DirsFirst),
            new("--sf --sort-files", "files first", (o, _) => o.Group = Grouping.FilesFirst),
        ]),
        ("Git",
        [
            new("--gs --git-status", "show git status (default: on inside a work tree)", (o, _) => o.Git = true),
            new("--no-git", "never run git", (o, _) => o.Git = false),
        ]),
        ("Other",
        [
            new("-h --help", "this text", (o, _) => o.Help = true),
            new("--version", "print the version", (o, _) => o.Version = true),
        ]),
    ];

    static readonly Dictionary<string, Flag> byName =
        sections.SelectMany(s => s.Flags).SelectMany(f => f.Names.Select(n => (n, f))).ToDictionary(x => x.n, x => x.f);

    static ArgumentException Invalid(string what, string? value) => new($"invalid {what} '{value}'");

    static ColorWhen ParseColor(string? value) => value switch
    {
        null or "always" => ColorWhen.Always,
        "auto" => ColorWhen.Auto,
        "never" => ColorWhen.Never,
        _ => throw Invalid("--color value", value),
    };

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a == "--") { o.paths.AddRange(args[(i + 1)..]); break; }
            if (a.StartsWith("--"))
            {
                var eq = a.IndexOf('=');
                var name = eq < 0 ? a : a[..eq];
                var value = eq < 0 ? null : a[(eq + 1)..];
                var flag = Find(name);
                if (value is not null && !flag.TakesValue) throw new ArgumentException($"option '{name}' takes no value");
                if (value is null && flag.NeedsValue) throw new ArgumentException($"option '{name}' needs a value");
                flag.Apply(o, value);
            }
            else if (a.Length > 1 && a[0] == '-')
                foreach (var c in a[1..]) Find($"-{c}").Apply(o, null);
            else o.paths.Add(a);
        }
        if (o.paths.Count == 0) o.paths.Add(".");
        return o;

        static Flag Find(string name) =>
            byName.TryGetValue(name, out var f) ? f : throw new ArgumentException($"unknown option '{name}'");
    }

    public static string HelpText()
    {
        var sb = new System.Text.StringBuilder("Usage: colorcls [options] [path ...]\n");
        foreach (var (title, flags) in sections)
        {
            sb.Append('\n').Append(title).Append('\n');
            foreach (var f in flags.Where(f => f.Help is not null))
            {
                var names = f.Spec.Replace(" ", ", ");
                if (f.Spec.StartsWith("--")) names = "    " + names;   // line long-only flags up under the long names
                sb.Append($"  {names,-24}{f.Help}\n");
            }
        }
        sb.Append("""

            Git column: ✓ clean, M modified, A added, D deleted, R renamed, U conflict,
            ? untracked, ! ignored. Directories show the union of what they contain.

            """);
        return sb.ToString();
    }
}
