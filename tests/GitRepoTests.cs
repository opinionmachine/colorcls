using System.Diagnostics;
using Xunit;

public class GitRepoTests
{
    // What `git status --porcelain=v1 -z --ignored -unormal` prints: NUL-separated "XY path" records.
    static readonly GitRepo repo = GitRepo.Parse("/repo", string.Join('\0',
        " M src/deep/x.py",
        "A  staged.rs",
        "AM both.rs",
        "R  docs/manual.md", "docs/guide.md",   // a rename is followed by the old path
        "?? new dir/",
        "!! build/",
        "!! docs/debug.log",
        ""));

    [Theory]
    [InlineData("src/deep/x.py", GitMarks.Modified)]
    [InlineData("staged.rs", GitMarks.Added)]
    [InlineData("both.rs", GitMarks.Added | GitMarks.Modified)]
    [InlineData("docs/manual.md", GitMarks.Renamed)]
    [InlineData("README.md", GitMarks.Clean)]
    [InlineData("docs/guide.md", GitMarks.Clean)]   // the old name of a rename is not a record of its own
    public void Files(string path, GitMarks expected) => Assert.Equal(expected, repo.Marks("/repo/" + path));

    [Theory]
    [InlineData("src", GitMarks.Modified)]
    [InlineData("src/deep", GitMarks.Modified)]
    [InlineData("src/other", GitMarks.Clean)]
    public void DirectoriesShowWhatChangedInside(string path, GitMarks expected) =>
        Assert.Equal(expected, repo.Marks("/repo/" + path));

    [Fact]
    public void HoldingAnIgnoredFileDoesNotMakeADirectoryIgnored()
    {
        Assert.Equal(GitMarks.Ignored, repo.Marks("/repo/docs/debug.log"));
        Assert.Equal(GitMarks.Renamed, repo.Marks("/repo/docs"));
    }

    [Theory]
    [InlineData("new dir", GitMarks.Untracked)]
    [InlineData("new dir/a.txt", GitMarks.Untracked)]
    [InlineData("new dir/sub/b.txt", GitMarks.Untracked)]
    [InlineData("build", GitMarks.Ignored)]
    [InlineData("build/out.bin", GitMarks.Ignored)]
    public void EverythingInsideAnUntrackedOrIgnoredDirectoryInheritsIt(string path, GitMarks expected) =>
        Assert.Equal(expected, repo.Marks("/repo/" + path));

    [Fact]
    public void TheRootCollectsEveryChangeExceptIgnored() =>
        Assert.Equal(GitMarks.Modified | GitMarks.Added | GitMarks.Renamed | GitMarks.Untracked, repo.Marks("/repo"));

    [Theory]
    [InlineData("/repo/.git")]
    [InlineData("/repo/.git/config")]
    [InlineData("/")]
    [InlineData("/elsewhere/file")]
    public void PathsGitDoesNotTrackHaveNoMarks(string path) => Assert.Null(repo.Marks(path));

    [Fact]
    public void DotGitPrefixedNamesAreOrdinaryFiles() => Assert.Equal(GitMarks.Clean, repo.Marks("/repo/.gitignore"));

    [Fact]
    public void MarksAreCountedAndOrderedForDisplay()
    {
        Assert.Equal(0, GitMarks.Clean.Count());
        Assert.Equal(2, (GitMarks.Added | GitMarks.Modified).Count());
        Assert.Equal("!?ACDMRTU", string.Concat(GitMark.All.Select(m => m.Letter)));
        Assert.Equal(GitMarks.Clean, GitMark.FromLetter(' '));
    }

    [Fact]
    public void ReadsARealWorkTree()
    {
        Assert.SkipUnless(Git(".", "--version"), "git is not installed");
        using var tmp = new Make.TempDir();
        Directory.CreateDirectory(tmp.At("src"));
        File.WriteAllText(tmp.At("src/a.cs"), "a");
        File.WriteAllText(tmp.At(".gitignore"), "*.log\n");
        Assert.True(Git(tmp.Path, "init", "-q") && Git(tmp.Path, "add", "-A")
            && Git(tmp.Path, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "init"));
        File.AppendAllText(tmp.At("src/a.cs"), "b");
        File.WriteAllText(tmp.At("new.txt"), "n");
        File.WriteAllText(tmp.At("debug.log"), "l");

        var found = GitRepo.Containing(tmp.At("src"));   // discovered from a subdirectory

        Assert.NotNull(found);
        Assert.Equal(GitMarks.Modified, found.Marks(tmp.At("src/a.cs")));
        Assert.Equal(GitMarks.Modified, found.Marks(tmp.At("src")));
        Assert.Equal(GitMarks.Untracked, found.Marks(tmp.At("new.txt")));
        Assert.Equal(GitMarks.Ignored, found.Marks(tmp.At("debug.log")));
        Assert.Equal(GitMarks.Clean, found.Marks(tmp.At(".gitignore")));
        Assert.Same(found, GitRepo.Containing(tmp.Path));   // one load per work tree
    }

    [Fact]
    public void ADirectoryOutsideAnyWorkTreeHasNoRepo()
    {
        using var tmp = new Make.TempDir();
        Assert.Null(GitRepo.Containing(tmp.Path));
    }

    static bool Git(string dir, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true };
            psi.ArgumentList.Add("-C");
            psi.ArgumentList.Add(dir);
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }
}
