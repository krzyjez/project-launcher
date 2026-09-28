using System.Diagnostics;
using Xunit;

namespace ProjectLauncher.Core.Tests;

public class GitStatusReaderTests
{
    [Fact]
    public void ParseStatus_reads_branch_upstream_and_clean_state()
    {
        var output = "# branch.oid 48ab2c22c27c17d4cfe0dd346fe3ea3c616d9859\n" +
                     "# branch.head main\n" +
                     "# branch.upstream origin/main\n" +
                     "# branch.ab +0 -0\n";

        var status = ProjectStatus(output);

        Assert.True(status.IsRepository);
        Assert.Equal("main", status.Branch);
        Assert.Equal("origin/main", status.Upstream);
        Assert.Equal("48ab2c22c27c17d4cfe0dd346fe3ea3c616d9859", status.HeadSha);
        Assert.False(status.IsDirty);
        Assert.False(status.IsDetached);
    }

    // Zmieniony i niesledzony plik tak samo oznaczaja brudne repozytorium.
    [Theory]
    [InlineData("1 .M N... 100644 100644 100644 abc abc readme.md")]
    [InlineData("? nowy-plik.txt")]
    [InlineData("u UU N... 100644 100644 100644 100644 a b c konflikt.cs")]
    public void ParseStatus_marks_any_file_entry_as_dirty(string entry)
    {
        var output = "# branch.oid 48ab2c2\r\n# branch.head main\r\n" + entry + "\r\n";

        Assert.True(ProjectStatus(output).IsDirty);
    }

    [Fact]
    public void ParseStatus_shows_short_sha_for_detached_head()
    {
        var output = "# branch.oid 48ab2c22c27c17d4cfe0dd346fe3ea3c616d9859\n# branch.head (detached)\n";

        var status = ProjectStatus(output);

        Assert.True(status.IsDetached);
        Assert.Equal("48ab2c2", status.Branch);
    }

    [Fact]
    public void ParseStatus_handles_repository_without_commits()
    {
        var status = ProjectStatus("# branch.oid (initial)\n# branch.head main\n");

        Assert.Equal("main", status.Branch);
        Assert.Equal("", status.HeadSha);
    }

    [Fact]
    public void ParseLsRemote_matches_exact_branch_ref()
    {
        var output = "111\trefs/heads/feature/main\n222\trefs/heads/main\n";

        Assert.Equal("222", GitStatusReader.ParseLsRemote(output, "main"));
        Assert.Null(GitStatusReader.ParseLsRemote(output, "develop"));
    }

    [Theory]
    [InlineData(true, GitSyncState.Pushed, GitStateKind.Dirty, "brudne")]
    [InlineData(false, GitSyncState.Pushed, GitStateKind.Clean, "czyste")]
    [InlineData(false, GitSyncState.Ahead, GitStateKind.CleanNotPushed, "czyste ↑2")]
    [InlineData(false, GitSyncState.NoRemoteBranch, GitStateKind.CleanNotPushed, "czyste · brak na GitHubie")]
    [InlineData(false, GitSyncState.Unknown, GitStateKind.Pending, "czyste")]
    public void State_kind_and_label_follow_dirty_flag_and_sync(bool isDirty, GitSyncState sync, GitStateKind kind, string label)
    {
        var status = new GitRepositoryStatus { IsDirty = isDirty, Sync = sync, AheadCount = 2 };

        Assert.Equal(kind, status.StateKind);
        Assert.Equal(label, status.StateLabel);
    }

    [Fact]
    public async Task ReadLocalAsync_returns_not_repository_for_plain_directory()
    {
        using var directory = new TempDirectory();

        var status = await GitStatusReader.ReadLocalAsync(directory.Path);

        Assert.False(status.IsRepository);
    }

    [Fact]
    public async Task Remote_check_reports_pushed_ahead_and_missing_branch()
    {
        using var directory = new TempDirectory();
        var origin = directory.Create("origin.git");
        var work = directory.Create("work");
        Git(origin, "init", "--bare", "--initial-branch=main");
        Git(work, "init", "--initial-branch=main");
        Git(work, "remote", "add", "origin", origin);
        Commit(work, "a.txt");

        // Galezi nie ma jeszcze na remote.
        Assert.Equal(GitSyncState.NoRemoteBranch, await ReadSyncAsync(work));

        Git(work, "push", "-u", "origin", "main");
        Assert.Equal(GitSyncState.Pushed, await ReadSyncAsync(work));

        Commit(work, "b.txt");
        Commit(work, "c.txt");
        var ahead = await GitStatusReader.ReadRemoteAsync(work, await GitStatusReader.ReadLocalAsync(work));
        Assert.Equal(GitSyncState.Ahead, ahead.Sync);
        Assert.Equal(2, ahead.AheadCount);

        File.WriteAllText(System.IO.Path.Combine(work, "d.txt"), "zmiana");
        var dirty = await GitStatusReader.ReadLocalAsync(work);
        Assert.True(dirty.IsDirty);
        Assert.Equal("main", dirty.Branch);
    }

    [Fact]
    public async Task Remote_check_reports_newer_work_on_remote()
    {
        using var directory = new TempDirectory();
        var origin = directory.Create("origin.git");
        var work = directory.Create("work");
        var other = directory.Create("other");
        Git(origin, "init", "--bare", "--initial-branch=main");
        Git(work, "init", "--initial-branch=main");
        Git(work, "remote", "add", "origin", origin);
        Commit(work, "a.txt");
        Git(work, "push", "-u", "origin", "main");

        // Ktos inny wypycha commit, ktorego lokalne repo jeszcze nie zna.
        Git(directory.Path, "clone", origin, other);
        Commit(other, "b.txt");
        Git(other, "push", "origin", "main");

        Assert.Equal(GitSyncState.RemoteNewer, await ReadSyncAsync(work));
    }

    [Fact]
    public async Task Remote_check_reports_repository_without_remote()
    {
        using var directory = new TempDirectory();
        var work = directory.Create("work");
        Git(work, "init", "--initial-branch=main");
        Commit(work, "a.txt");

        Assert.Equal(GitSyncState.NoRemote, await ReadSyncAsync(work));
    }

    [Fact]
    public void ParseWorktrees_skips_current_bare_and_prunable_entries()
    {
        var output = "worktree P:/repo\nHEAD 111\nbranch refs/heads/main\n\n" +
                     "worktree P:/repo-feature\nHEAD 222\nbranch refs/heads/feature/login\n\n" +
                     "worktree P:/repo-detached\nHEAD 3333333333\ndetached\n\n" +
                     "worktree P:/repo-gone\nHEAD 444\nbranch refs/heads/old\nprunable gitdir file points to non-existent location\n\n" +
                     "worktree P:/repo.git\nbare\n";

        var worktrees = GitStatusReader.ParseWorktrees(output, @"P:\repo\");

        Assert.Equal(2, worktrees.Count);
        Assert.Equal("feature/login", worktrees[0].Branch);
        Assert.EndsWith("repo-feature", worktrees[0].Path);
        Assert.Equal("3333333", worktrees[1].Branch);
    }

    [Fact]
    public async Task ReadLocalAsync_lists_other_worktrees_from_both_sides()
    {
        using var directory = new TempDirectory();
        var main = directory.Create("main");
        var linked = System.IO.Path.Combine(directory.Path, "linked");
        Git(main, "init", "--initial-branch=main");
        Commit(main, "a.txt");
        Git(main, "worktree", "add", "-b", "feature", linked);

        var fromMain = await GitStatusReader.ReadLocalAsync(main);
        var fromLinked = await GitStatusReader.ReadLocalAsync(linked);

        Assert.Equal("feature", Assert.Single(fromMain.Worktrees).Branch);
        Assert.Equal("feature", fromLinked.Branch);
        Assert.Equal("main", Assert.Single(fromLinked.Worktrees).Branch);
    }

    [Fact]
    public async Task Worktree_has_own_dirty_flag_and_task_description_with_commit_fallback()
    {
        using var directory = new TempDirectory();
        var main = directory.Create("main");
        var withTask = System.IO.Path.Combine(directory.Path, "with-task");
        var withoutTask = System.IO.Path.Combine(directory.Path, "without-task");
        Git(main, "init", "--initial-branch=main");
        Commit(main, "a.txt");
        Git(main, "worktree", "add", "-b", "analiza", withTask);
        Git(main, "worktree", "add", "-b", "poprawki", withoutTask);
        Commit(withoutTask, "zażółć.txt");
        WriteBranchState(withTask, "analiza", "Analiza rozmów i kategoryzacja");
        File.WriteAllText(System.IO.Path.Combine(withTask, "roboczy.txt"), "zmiana");

        var status = await GitStatusReader.ReadLocalAsync(main);

        var task = status.Worktrees.Single(worktree => worktree.Branch == "analiza");
        var fallback = status.Worktrees.Single(worktree => worktree.Branch == "poprawki");
        Assert.True(task.IsDirty);
        Assert.Equal("Analiza rozmów i kategoryzacja", task.Description);
        Assert.False(fallback.IsDirty);
        Assert.Equal("zażółć.txt", fallback.Description);
    }

    [Fact]
    public void Branch_task_description_is_ignored_for_other_branch_or_broken_file()
    {
        using var directory = new TempDirectory();
        WriteBranchState(directory.Path, "two-columns", "Nowy layout");

        Assert.Equal("Nowy layout", GitStatusReader.ReadBranchTaskDescription(directory.Path, "two-columns"));
        Assert.Equal("", GitStatusReader.ReadBranchTaskDescription(directory.Path, "main"));

        File.WriteAllText(System.IO.Path.Combine(directory.Path, ".workai", "branch-state.json"), "{ niepoprawny");
        Assert.Equal("", GitStatusReader.ReadBranchTaskDescription(directory.Path, "two-columns"));
    }

    // Minimalny stan programu branch: tylko pola, ktore czyta launcher.
    private static void WriteBranchState(string worktreePath, string branch, string description)
    {
        var workai = Directory.CreateDirectory(System.IO.Path.Combine(worktreePath, ".workai")).FullName;
        var json = System.Text.Json.JsonSerializer.Serialize(new { BranchName = branch, StartDescription = description });
        File.WriteAllText(System.IO.Path.Combine(workai, "branch-state.json"), json);
    }

    private static GitRepositoryStatus ProjectStatus(string output) => GitStatusReader.ParseStatus(output);

    private static async Task<GitSyncState> ReadSyncAsync(string repositoryPath)
    {
        var local = await GitStatusReader.ReadLocalAsync(repositoryPath);
        return (await GitStatusReader.ReadRemoteAsync(repositoryPath, local)).Sync;
    }

    private static void Commit(string repositoryPath, string fileName)
    {
        File.WriteAllText(System.IO.Path.Combine(repositoryPath, fileName), fileName);
        Git(repositoryPath, "add", fileName);
        Git(repositoryPath, "-c", "user.name=Test", "-c", "user.email=test@example.com", "commit", "-m", fileName);
    }

    private static void Git(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        var error = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', arguments)}: {error.Result}");
    }

    // Katalog tymczasowy usuwany po tescie; pliki obiektow git sa tylko do odczytu, wiec zdejmujemy atrybut.
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("launcher-git-").FullName;

        public string Create(string name) => Directory.CreateDirectory(System.IO.Path.Combine(Path, name)).FullName;

        public void Dispose()
        {
            foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);

            Directory.Delete(Path, recursive: true);
        }
    }
}
