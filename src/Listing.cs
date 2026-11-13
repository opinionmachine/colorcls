/// <summary>Reads the file system into entries and decides which to show in what order. No output.</summary>
static class Listing
{
    public static List<Entry> ReadDirectory(string dir, Options o, bool withDots)
    {
        var repo = o.Git ? GitRepo.Containing(dir) : null;
        var info = new DirectoryInfo(dir);
        var entries = new List<Entry>();
        if (withDots)
        {
            entries.Add(Entry.Probe(info, ".", ".", repo));
            entries.Add(Entry.Probe(new DirectoryInfo(Path.Combine(info.FullName, "..")), "..", "..", repo));
        }
        foreach (var fi in info.EnumerateFileSystemInfos()) entries.Add(Entry.Probe(fi, fi.Name, fi.Name, repo));
        return entries;
    }

    /// <summary>A non-directory named on the command line, shown under the path as typed. Null if it does not exist.</summary>
    public static Entry? ReadArgument(string path, Options o)
    {
        var fi = new FileInfo(path);
        if (!fi.Exists && fi.LinkTarget is null) return null;
        var repo = o.Git && fi.DirectoryName is { } parent ? GitRepo.Containing(parent) : null;
        return Entry.Probe(fi, fi.Name, path, repo);
    }

    public static IEnumerable<Entry> Filter(IEnumerable<Entry> entries, Options o)
    {
        if (o.Hidden == Hidden.Skip) entries = entries.Where(e => !e.Name.StartsWith('.'));
        if (o.DirsOnly) entries = entries.Where(e => e.IsDir);
        if (o.FilesOnly) entries = entries.Where(e => !e.IsDir);
        return entries;
    }

    public static List<Entry> Sort(IEnumerable<Entry> entries, Options o)
    {
        var byName = StringComparer.OrdinalIgnoreCase;
        var sorted = o.Sort switch
        {
            SortKey.Time => entries.OrderByDescending(e => e.MTime).ThenBy(e => e.Display, byName),
            SortKey.Size => entries.OrderByDescending(e => e.Size).ThenBy(e => e.Display, byName),
            SortKey.Extension => entries.OrderBy(e => e.Extension, byName).ThenBy(e => e.Display, byName),
            SortKey.None => entries,
            _ => entries.OrderBy(e => e.Display.TrimStart('.'), byName).ThenBy(e => e.Display, byName),   // dotfiles sort with their name
        };
        if (o.Reverse) sorted = sorted.Reverse();
        if (o.Group != Grouping.None)
            sorted = sorted.OrderBy(e => e.IsDir == (o.Group == Grouping.DirsFirst) ? 0 : 1);   // stable: keeps the order within each group
        return sorted.ToList();
    }
}
