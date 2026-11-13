using System.ComponentModel;
using System.Diagnostics;
using System.Text;

/// <summary>What git says about a path. Declared in the order the marks are printed.</summary>
[Flags]
public enum GitMarks
{
    Clean = 0,
    Ignored = 1 << 0,
    Untracked = 1 << 1,
    Added = 1 << 2,
    Copied = 1 << 3,
    Deleted = 1 << 4,
    Modified = 1 << 5,
    Renamed = 1 << 6,
    TypeChanged = 1 << 7,
    Conflict = 1 << 8,
}

static class GitMark
{
    public static readonly (GitMarks Mark, char Letter)[] All =
    [
        (GitMarks.Ignored, '!'), (GitMarks.Untracked, '?'), (GitMarks.Added, 'A'), (GitMarks.Copied, 'C'),
        (GitMarks.Deleted, 'D'), (GitMarks.Modified, 'M'), (GitMarks.Renamed, 'R'), (GitMarks.TypeChanged, 'T'),
        (GitMarks.Conflict, 'U'),
    ];

    public static GitMarks FromLetter(char letter) => All.FirstOrDefault(m => m.Letter == letter).Mark;

    public static int Count(this GitMarks marks) => System.Numerics.BitOperations.PopCount((uint)marks);
}

/// <summary>One work tree's status, loaded with a single `git status` and queried by path.</summary>
sealed class GitRepo
{
    static readonly Dictionary<string, GitRepo?> byRoot = [];

    readonly string root;
    // Repo-relative path ("" is the root) → marks, for every path git reported and each directory above it.
    readonly Dictionary<string, GitMarks> reported = [];
    // Untracked or ignored directories, which git reports as one record without listing what is inside.
    readonly Dictionary<string, GitMarks> collapsed = [];

    GitRepo(string root) => this.root = root;

    /// <summary>The work tree a directory belongs to: the nearest ancestor holding a .git entry.</summary>
    public static GitRepo? Containing(string dir)
    {
        for (var d = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir)); d is not null; d = Path.GetDirectoryName(d))
        {
            var dotGit = Path.Combine(d, ".git");
            if (!Directory.Exists(dotGit) && !File.Exists(dotGit)) continue;
            return byRoot.TryGetValue(d, out var known) ? known : byRoot[d] = Load(d);
        }
        return null;
    }

    /// <summary>Null for paths git does not track at all: outside the work tree, or inside .git.</summary>
    public GitMarks? Marks(string fullPath)
    {
        var rel = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
        if (rel == ".") rel = "";
        if (rel == ".." || rel.StartsWith("../") || rel == ".git" || rel.StartsWith(".git/")) return null;

        if (reported.TryGetValue(rel, out var marks)) return marks;
        for (var dir = Parent(rel); dir is not null; dir = Parent(dir))
            if (collapsed.TryGetValue(dir, out marks)) return marks;
        return GitMarks.Clean;
    }

    static string? Parent(string rel) => rel.Length == 0 ? null : rel[..Math.Max(0, rel.LastIndexOf('/'))];

    static GitRepo? Load(string root) =>
        Run(root, "status", "--porcelain=v1", "-z", "--ignored", "-unormal") is { } status ? Parse(root, status) : null;

    /// <summary>Builds the lookup from `git status --porcelain=v1 -z` output.</summary>
    public static GitRepo Parse(string root, string status)
    {
        var repo = new GitRepo(root);
        var records = status.Split('\0');
        for (int i = 0; i < records.Length; i++)
        {
            var record = records[i];   // "XY path"
            if (record.Length < 4) continue;
            var marks = GitMark.FromLetter(record[0]) | GitMark.FromLetter(record[1]);
            var path = record[3..];
            if (record[0] is 'R' or 'C') i++;   // the next record is the path it was renamed from

            if (path.EndsWith('/'))
            {
                path = path.TrimEnd('/');
                repo.collapsed[path] = marks;
            }
            repo.reported[path] = repo.reported.GetValueOrDefault(path) | marks;

            // A directory shows what changed inside it, but holding an ignored file does not make it ignored.
            var inherited = marks & ~GitMarks.Ignored;
            for (var dir = Parent(path); dir is not null && inherited != 0; dir = Parent(dir))
                repo.reported[dir] = repo.reported.GetValueOrDefault(dir) | inherited;
        }
        return repo;
    }

    static string? Run(string dir, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
            };
            psi.Environment["GIT_OPTIONAL_LOCKS"] = "0";   // a listing must never take the index lock
            psi.ArgumentList.Add("-C");
            psi.ArgumentList.Add(dir);
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            p.ErrorDataReceived += (_, _) => { };   // drain stderr so git cannot block on it
            p.BeginErrorReadLine();
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode == 0 ? stdout : null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException) { return null; }
    }
}
