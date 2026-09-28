namespace ProjectLauncher.Core;

/// <summary>Stan galezi wzgledem GitHuba (albo innego remote), ustalany przez `git ls-remote`</summary>
public enum GitSyncState
{
    /// <summary>Sprawdzanie jeszcze sie nie zakonczylo albo nie ma czego sprawdzac (np. detached HEAD)</summary>
    Unknown,
    Pushed,
    Ahead,
    RemoteNewer,
    NoRemoteBranch,
    NoRemote,
    Unavailable
}

/// <summary>Uproszczony stan repozytorium do pokazania na karcie projektu</summary>
public enum GitStateKind
{
    Dirty,
    Clean,
    CleanNotPushed,
    Pending
}

/// <summary>Dodatkowy worktree repozytorium, ktory mozna otworzyc w edytorze obok glownego katalogu projektu</summary>
public sealed record GitWorktree(string Path, string Branch)
{
    /// <summary>Worktree nie trafia na GitHuba, wiec jego stan to tylko czystosc katalogu roboczego</summary>
    public bool IsDirty { get; init; }

    /// <summary>Opis zadania z workflow `branch`, a bez niego temat ostatniego commita</summary>
    public string Description { get; init; } = "";

    public string StateLabel => IsDirty ? "brudne" : "czyste";
}

/// <summary>Migawka stanu repozytorium projektu: biezaca galaz, czystosc worktree i synchronizacja z remote</summary>
public sealed record GitRepositoryStatus
{
    /// <summary>Katalog projektu nie jest repozytorium Git albo Git nie jest dostepny</summary>
    public static GitRepositoryStatus NotRepository { get; } = new() { IsRepository = false };

    public bool IsRepository { get; init; } = true;

    /// <summary>Nazwa biezacej galezi; przy detached HEAD skrocony SHA commita</summary>
    public string Branch { get; init; } = "";

    public bool IsDetached { get; init; }

    public string HeadSha { get; init; } = "";

    /// <summary>Galaz sledzona w formacie `remote/galaz`; pusta, gdy nie ustawiono upstream</summary>
    public string Upstream { get; init; } = "";

    public bool IsDirty { get; init; }

    public GitSyncState Sync { get; init; } = GitSyncState.Unknown;

    /// <summary>Liczba lokalnych commitow, ktorych nie ma w remote; znaczace przy `Sync == Ahead`</summary>
    public int AheadCount { get; init; }

    /// <summary>Opis zadania aktywnego workflow `branch` na biezacej galezi; pusty, gdy galaz nie ma takiego zadania</summary>
    public string TaskDescription { get; init; } = "";

    /// <summary>Pozostale worktree repozytorium, bez tego, w ktorym lezy katalog projektu</summary>
    public IReadOnlyList<GitWorktree> Worktrees { get; init; } = [];

    /// <summary>Kategoria stanu decydujaca o kolorze flagi na karcie</summary>
    public GitStateKind StateKind
    {
        get
        {
            if (IsDirty)
                return GitStateKind.Dirty;

            return Sync switch
            {
                GitSyncState.Unknown => GitStateKind.Pending,
                GitSyncState.Pushed => GitStateKind.Clean,
                _ => GitStateKind.CleanNotPushed
            };
        }
    }

    /// <summary>Krotki opis stanu pokazywany obok nazwy galezi</summary>
    public string StateLabel
    {
        get
        {
            if (IsDirty)
                return "brudne";

            return Sync switch
            {
                GitSyncState.Pushed => "czyste",
                GitSyncState.Ahead => $"czyste ↑{AheadCount}",
                GitSyncState.RemoteNewer => "czyste ↓ nowsze na GitHubie",
                GitSyncState.NoRemoteBranch => "czyste · brak na GitHubie",
                GitSyncState.NoRemote => "czyste · bez GitHuba",
                GitSyncState.Unavailable => "czyste · GitHub niedostepny",
                _ => "czyste"
            };
        }
    }
}
