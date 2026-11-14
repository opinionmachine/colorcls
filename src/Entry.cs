public enum EntryKind { File, Directory, Symlink, Fifo, Socket, CharDevice, BlockDevice }

sealed class Entry
{
    const UnixFileMode AnyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    /// <summary>The file name: what git, the icon tables and the dotfile filter look at.</summary>
    public required string Name { get; init; }
    /// <summary>What gets printed and sorted on: the name, or the path as typed for a command line argument.</summary>
    public required string Display { get; init; }
    public required string FullPath { get; init; }
    public EntryKind Kind { get; init; }
    public UnixFileMode Mode { get; init; }
    public uint? Uid { get; init; }
    public uint? Gid { get; init; }
    public long Size { get; init; }
    /// <summary>Modification time in UTC: converting to local time is left to the one column that prints it.</summary>
    public DateTime MTime { get; init; }
    public string? LinkTarget { get; init; }
    public bool IsDead { get; init; }
    /// <summary>Null when the entry is not inside a git work tree.</summary>
    public GitMarks? Git { get; init; }

    public bool IsDir => Kind == EntryKind.Directory;
    public bool IsLink => Kind == EntryKind.Symlink;
    public bool IsExec => Kind == EntryKind.File && (Mode & AnyExecute) != 0;
    public string Extension => IsDir ? "" : Path.GetExtension(Name).TrimStart('.').ToLowerInvariant();
    public string Owner => Uid is { } id ? Posix.UserName(id) : "-";
    public string Group => Gid is { } id ? Posix.GroupName(id) : "-";
    public (string Glyph, bool Recognised) Icon => icon ??= Icons.For(this);

    (string, bool)? icon;

    public static Entry Probe(FileSystemInfo fi, string name, string display, GitRepo? repo)
    {
        var git = repo?.Marks(fi.FullName);
        try
        {
            // lstat is the single source for type, permissions and ownership; the BCL covers Windows.
            var stat = Posix.Lstat(fi.FullName);
            var target = fi.LinkTarget;
            var kind = stat?.Kind
                ?? (target is not null ? EntryKind.Symlink
                    : fi.Attributes.HasFlag(FileAttributes.Directory) ? EntryKind.Directory : EntryKind.File);
            return new Entry
            {
                Name = name, Display = display, FullPath = fi.FullName, Git = git,
                Kind = kind,
                Mode = stat?.Permissions ?? (OperatingSystem.IsWindows() ? UnixFileMode.None : fi.UnixFileMode),
                Uid = stat?.Uid,
                Gid = stat?.Gid,
                Size = fi is FileInfo f ? f.Length : 0,
                MTime = fi.LastWriteTimeUtc,
                LinkTarget = target,
                IsDead = kind == EntryKind.Symlink && !TargetExists(fi),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Vanished or unreadable between listing and probing: still show the name.
            return new Entry { Name = name, Display = display, FullPath = fi.FullName, Git = git };
        }
    }

    static bool TargetExists(FileSystemInfo link)
    {
        try { return link.ResolveLinkTarget(returnFinalTarget: true)?.Exists ?? false; }
        catch (IOException) { return false; }   // dangling or looping
    }
}
