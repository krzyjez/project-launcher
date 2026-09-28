using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace ProjectLauncher.Core;

/// <summary>Odczytuje stan repozytorium przez `git`; odczyt lokalny jest szybki, sprawdzenie remote wymaga sieci</summary>
public static class GitStatusReader
{
    private static readonly TimeSpan LocalTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RemoteTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Czyta biezaca galaz, czystosc worktree i upstream bez kontaktu z siecia</summary>
    public static async Task<GitRepositoryStatus> ReadLocalAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(repositoryPath))
            return GitRepositoryStatus.NotRepository;

        var result = await _RunGitAsync(repositoryPath, ["status", "--porcelain=v2", "--branch"], LocalTimeout, cancellationToken);
        if (result.ExitCode != 0)
            return GitRepositoryStatus.NotRepository;

        var status = ParseStatus(result.Output);
        var topLevel = await _RunGitAsync(repositoryPath, ["rev-parse", "--show-toplevel"], LocalTimeout, cancellationToken);
        if (topLevel.ExitCode != 0)
            return status;

        var topLevelPath = topLevel.Output.Trim();
        status = status with { TaskDescription = ReadBranchTaskDescription(topLevelPath, status.Branch) };

        var worktrees = await _RunGitAsync(repositoryPath, ["worktree", "list", "--porcelain"], LocalTimeout, cancellationToken);
        if (worktrees.ExitCode != 0)
            return status;

        var details = await Task.WhenAll(ParseWorktrees(worktrees.Output, topLevelPath)
            .Select(worktree => _ReadWorktreeDetailsAsync(worktree, cancellationToken)));

        return status with { Worktrees = details };
    }

    /// <summary>Czyta opis zadania z `.workai/branch-state.json` programu `branch`; pusty, gdy plik nie istnieje albo dotyczy innej galezi</summary>
    public static string ReadBranchTaskDescription(string worktreePath, string branch)
    {
        var statePath = System.IO.Path.Combine(worktreePath, ".workai", "branch-state.json");
        if (!File.Exists(statePath))
            return "";

        try
        {
            // Program branch moze w tej chwili zapisywac plik, wiec nie blokujemy go przy odczycie.
            using var stream = new FileStream(statePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            // Stan po zamknietym workflow albo z innej galezi nie opisuje tego, co jest teraz w katalogu.
            if (!root.TryGetProperty("BranchName", out var branchName) || branchName.GetString() != branch)
                return "";

            return root.TryGetProperty("StartDescription", out var description)
                ? description.GetString()?.Trim() ?? ""
                : "";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return "";
        }
    }

    // Worktree ma wlasny stan czystosci i wlasne zadanie; bez zadania pokazujemy temat ostatniego commita.
    private static async Task<GitWorktree> _ReadWorktreeDetailsAsync(GitWorktree worktree, CancellationToken cancellationToken)
    {
        var status = await _RunGitAsync(worktree.Path, ["status", "--porcelain"], LocalTimeout, cancellationToken);
        var description = ReadBranchTaskDescription(worktree.Path, worktree.Branch);
        if (description.Length == 0)
        {
            var lastCommit = await _RunGitAsync(worktree.Path, ["log", "-1", "--format=%s"], LocalTimeout, cancellationToken);
            description = lastCommit.ExitCode == 0 ? lastCommit.Output.Trim() : "";
        }

        return worktree with
        {
            IsDirty = status.ExitCode == 0 && status.Output.Trim().Length > 0,
            Description = description
        };
    }

    /// <summary>Parsuje `git worktree list --porcelain`, pomijajac biezacy worktree, repozytoria bare i usuniete katalogi</summary>
    public static IReadOnlyList<GitWorktree> ParseWorktrees(string porcelainOutput, string currentWorktreePath)
    {
        var worktrees = new List<GitWorktree>();
        var current = _NormalizePath(currentWorktreePath);

        foreach (var block in porcelainOutput.Replace("\r", "").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var path = lines.FirstOrDefault(line => line.StartsWith("worktree ", StringComparison.Ordinal))?["worktree ".Length..];
            if (path is null || lines.Any(line => line == "bare" || line.StartsWith("prunable", StringComparison.Ordinal)))
                continue;

            if (string.Equals(_NormalizePath(path), current, StringComparison.OrdinalIgnoreCase))
                continue;

            var branchRef = lines.FirstOrDefault(line => line.StartsWith("branch ", StringComparison.Ordinal))?["branch ".Length..];
            var head = lines.FirstOrDefault(line => line.StartsWith("HEAD ", StringComparison.Ordinal))?["HEAD ".Length..] ?? "";
            var branch = branchRef is not null
                ? branchRef.Replace("refs/heads/", "")
                : head.Length >= 7 ? head[..7] : head;

            worktrees.Add(new GitWorktree(path.Replace('/', System.IO.Path.DirectorySeparatorChar), branch));
        }

        return worktrees;
    }

    /// <summary>Porownuje lokalny HEAD z galezia na remote przez `git ls-remote`, ktory niczego nie zapisuje w repozytorium</summary>
    public static async Task<GitRepositoryStatus> ReadRemoteAsync(
        string repositoryPath,
        GitRepositoryStatus local,
        CancellationToken cancellationToken = default)
    {
        if (!local.IsRepository || local.IsDetached || local.HeadSha.Length == 0)
            return local;

        var (remote, remoteBranch) = await _ResolveRemoteBranchAsync(repositoryPath, local, cancellationToken);
        if (remote is null)
            return local with { Sync = GitSyncState.NoRemote };

        var lsRemote = await _RunGitAsync(
            repositoryPath,
            ["ls-remote", "--heads", remote, remoteBranch],
            RemoteTimeout,
            cancellationToken);

        if (lsRemote.ExitCode != 0)
            return local with { Sync = GitSyncState.Unavailable };

        var remoteSha = ParseLsRemote(lsRemote.Output, remoteBranch);
        if (remoteSha is null)
            return local with { Sync = GitSyncState.NoRemoteBranch };

        if (string.Equals(remoteSha, local.HeadSha, StringComparison.OrdinalIgnoreCase))
            return local with { Sync = GitSyncState.Pushed };

        // Commit z remote, ktorego nie ma lokalnie albo ktory nie jest przodkiem HEAD, oznacza nowsza prace na GitHubie.
        var isAncestor = await _RunGitAsync(
            repositoryPath,
            ["merge-base", "--is-ancestor", remoteSha, "HEAD"],
            LocalTimeout,
            cancellationToken);

        if (isAncestor.ExitCode != 0)
            return local with { Sync = GitSyncState.RemoteNewer };

        var count = await _RunGitAsync(
            repositoryPath,
            ["rev-list", "--count", $"{remoteSha}..HEAD"],
            LocalTimeout,
            cancellationToken);

        return local with
        {
            Sync = GitSyncState.Ahead,
            AheadCount = int.TryParse(count.Output.Trim(), out var ahead) ? ahead : 0
        };
    }

    /// <summary>Parsuje wynik `git status --porcelain=v2 --branch`</summary>
    public static GitRepositoryStatus ParseStatus(string porcelainOutput)
    {
        var headSha = "";
        var branch = "";
        var upstream = "";
        var isDirty = false;

        foreach (var rawLine in porcelainOutput.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
                continue;

            if (!line.StartsWith('#'))
            {
                // Kazdy wpis poza naglowkami to zmieniony, nowy albo konfliktowy plik.
                isDirty = true;
                continue;
            }

            if (_TryReadHeader(line, "# branch.oid ", out var oid))
                headSha = oid == "(initial)" ? "" : oid;
            else if (_TryReadHeader(line, "# branch.head ", out var head))
                branch = head;
            else if (_TryReadHeader(line, "# branch.upstream ", out var tracked))
                upstream = tracked;
        }

        var isDetached = branch == "(detached)";
        return new GitRepositoryStatus
        {
            Branch = isDetached && headSha.Length >= 7 ? headSha[..7] : branch,
            IsDetached = isDetached,
            HeadSha = headSha,
            Upstream = upstream,
            IsDirty = isDirty
        };
    }

    /// <summary>Zwraca SHA galezi z wyniku `git ls-remote --heads`; null, gdy remote nie ma tej galezi</summary>
    public static string? ParseLsRemote(string lsRemoteOutput, string branch)
    {
        var expectedRef = $"refs/heads/{branch}";
        foreach (var rawLine in lsRemoteOutput.Split('\n'))
        {
            var parts = rawLine.TrimEnd('\r').Split('\t');
            if (parts.Length == 2 && parts[1] == expectedRef)
                return parts[0];
        }

        return null;
    }

    // Upstream wskazuje remote i galaz wprost; bez niego zakladamy `origin` i te sama nazwe galezi.
    private static async Task<(string? Remote, string Branch)> _ResolveRemoteBranchAsync(
        string repositoryPath,
        GitRepositoryStatus local,
        CancellationToken cancellationToken)
    {
        var remotes = await _RunGitAsync(repositoryPath, ["remote"], LocalTimeout, cancellationToken);
        var remoteNames = remotes.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (remoteNames.Count == 0)
            return (null, local.Branch);

        // Nazwa remote moze zawierac `/`, wiec dopasowujemy najdluzszy znany prefiks.
        var upstreamRemote = remoteNames
            .Where(name => local.Upstream.StartsWith(name + "/", StringComparison.Ordinal))
            .OrderByDescending(name => name.Length)
            .FirstOrDefault();

        if (upstreamRemote is not null)
            return (upstreamRemote, local.Upstream[(upstreamRemote.Length + 1)..]);

        var fallbackRemote = remoteNames.Contains("origin") ? "origin" : remoteNames[0];
        return (fallbackRemote, local.Branch);
    }

    // Git podaje sciezki z `/`; porownanie ma byc odporne na separatory i koncowy ukosnik.
    private static string _NormalizePath(string path)
    {
        return path.Trim().Replace('\\', '/').TrimEnd('/');
    }

    private static bool _TryReadHeader(string line, string prefix, out string value)
    {
        value = line.StartsWith(prefix, StringComparison.Ordinal) ? line[prefix.Length..] : "";
        return value.Length > 0;
    }

    // Uruchamia git bez okna konsoli i bez interaktywnych pytan o dane logowania.
    private static async Task<(int ExitCode, string Output)> _RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            // Git wypisuje tematy commitow i sciezki w UTF-8; domyslna strona kodowa Windows psulaby polskie znaki.
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(workingDirectory);
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        // Launcher czyta repozytoria, w ktorych rownolegle pracuja agenci: status nie moze zakladac blokady indeksu.
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GCM_INTERACTIVE"] = "never";
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GIT_SSH_COMMAND")))
            startInfo.Environment["GIT_SSH_COMMAND"] = "ssh -o BatchMode=yes";

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        Process process;
        try
        {
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("Nie udalo sie uruchomic git.");
        }
        catch (Win32Exception)
        {
            // Brak git w PATH traktujemy tak samo jak katalog bez repozytorium.
            return (-1, "");
        }

        using (process)
        {
            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeoutSource.Token);

            try
            {
                await process.WaitForExitAsync(timeoutSource.Token);
                return (process.ExitCode, await outputTask);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Proces zdazyl sie zakonczyc miedzy anulowaniem a proba zabicia.
                }

                if (cancellationToken.IsCancellationRequested)
                    throw;

                return (-1, "");
            }
            finally
            {
                // Strumien bledow czytamy tylko po to, zeby git nie zablokowal sie na pelnym buforze.
                _ = errorTask.ContinueWith(_ => { }, TaskScheduler.Default);
            }
        }
    }
}
