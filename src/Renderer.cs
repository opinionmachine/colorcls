/// <summary>The four layouts. Each takes entries that are already filtered and sorted and writes them out.</summary>
sealed class Renderer(TextWriter output, Columns columns, Terminal term)
{
    static readonly Cell Gap = Cell.Space(2);

    public void Render(Layout layout, List<Entry> entries, int treeDepth, Func<string, List<Entry>?> children)
    {
        if (entries.Count == 0) return;
        switch (layout)
        {
            case Layout.Long: Long(entries); break;
            case Layout.Tree: Tree(entries, treeDepth, children, indent: ""); break;
            case Layout.OnePerLine: foreach (var cell in Named(entries)) output.WriteLine(cell.Text); break;
            default: Grid(Named(entries)); break;
        }
    }

    /// <summary>"git icon  name" for each entry; the git column only appears inside a work tree.</summary>
    List<Cell> Named(List<Entry> entries)
    {
        int git = Columns.GitWidth(entries);
        return entries.ConvertAll(e => git > 0 ? columns.Git(e.Git, git) + Cell.Space(1) + columns.Label(e) : columns.Label(e));
    }

    // Column-major, like ls: as many columns as fit the terminal.
    void Grid(List<Cell> cells)
    {
        int n = cells.Count, rows = n;
        int[] widths = [cells.Max(c => c.Width)];
        for (int cols = Math.Min(n, Math.Max(1, term.Width / 3)); cols > 1; cols--)
        {
            int r = (n + cols - 1) / cols, used = (n + r - 1) / r;
            var w = new int[used];
            for (int i = 0; i < n; i++) w[i / r] = Math.Max(w[i / r], cells[i].Width);
            if (w.Sum() + Gap.Width * (used - 1) > term.Width) continue;
            (rows, widths) = (r, w);
            break;
        }
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < widths.Length && c * rows + r < n; c++)
            {
                int i = c * rows + r;
                bool lastInRow = i + rows >= n;
                output.Write(lastInRow ? cells[i].Text : (cells[i].PadRight(widths[c]) + Gap).Text);
            }
            output.WriteLine();
        }
    }

    void Long(List<Entry> entries)
    {
        bool ownership = !OperatingSystem.IsWindows();
        int owner = entries.Max(e => TextWidth.Of(e.Owner)), group = entries.Max(e => TextWidth.Of(e.Group));
        int git = Columns.GitWidth(entries);
        foreach (var e in entries)
        {
            var row = Cell.Space(0);
            if (ownership)
                row += columns.Mode(e) + Gap + columns.Owner(e).PadRight(owner) + Gap + columns.Group(e).PadRight(group) + Gap;
            row += columns.Size(e) + Gap + columns.Time(e) + Gap;
            if (git > 0) row += columns.Git(e.Git, git) + Gap;
            output.WriteLine((row + columns.Label(e) + columns.LinkTarget(e)).Text);
        }
    }

    void Tree(List<Entry> entries, int depth, Func<string, List<Entry>?> children, string indent)
    {
        int git = Columns.GitWidth(entries);
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            bool last = i == entries.Count - 1;
            var row = git > 0 ? columns.Git(e.Git, git) + Cell.Space(1) : Cell.Space(0);
            row += term.Paint(Theme.Tree, indent + (last ? " └── " : " ├── ")) + columns.Label(e);
            output.WriteLine(row.Text);

            if (e.IsDir && depth > 1 && children(e.FullPath) is { Count: > 0 } inside)
                Tree(inside, depth - 1, children, indent + (last ? "     " : " │   "));
        }
    }
}
