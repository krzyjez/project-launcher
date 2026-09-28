namespace ProjectLauncher.Core;

/// <summary>Tlumaczy sciezki projektow miedzy zapisem Windows a zapisem Linux; rejestr przechowuje zapis Windows</summary>
public static class ProjectPathTranslator
{
    /// <summary>Domyslny punkt montowania dla litery dysku: `P:` odpowiada katalogowi `/p`</summary>
    public static string DefaultMountPoint(char driveLetter)
    {
        return "/" + char.ToLowerInvariant(driveLetter);
    }

    /// <summary>Zamienia sciezke z rejestru na zapis rozumiany przez biezacy system</summary>
    public static string ToCurrentSystem(string registryPath, IReadOnlyDictionary<char, string>? mountPoints = null)
    {
        return OperatingSystem.IsWindows() ? registryPath : ToLinux(registryPath, mountPoints);
    }

    /// <summary>Zamienia sciezke z biezacego systemu na kanoniczny zapis Windows zapisywany w rejestrze</summary>
    public static string ToRegistryPath(string systemPath, IReadOnlyDictionary<char, string>? mountPoints = null)
    {
        return OperatingSystem.IsWindows() ? systemPath : ToWindows(systemPath, mountPoints);
    }

    /// <summary>Zamienia `P:\ai` na `/p/ai`; sciezki sieciowe i wzgledne zostawia bez zmian</summary>
    public static string ToLinux(string windowsPath, IReadOnlyDictionary<char, string>? mountPoints = null)
    {
        if (string.IsNullOrWhiteSpace(windowsPath) || windowsPath.StartsWith('/'))
            return windowsPath;

        // Bez litery dysku nie ma czego tlumaczyc: sciezka sieciowa albo wzgledna.
        if (windowsPath.Length < 2 || windowsPath[1] != ':' || !char.IsLetter(windowsPath[0]))
            return windowsPath;

        var mountPoint = _Lookup(mountPoints, windowsPath[0]) ?? DefaultMountPoint(windowsPath[0]);
        var rest = windowsPath[2..].Replace('\\', '/').TrimStart('/');

        return rest.Length == 0 ? mountPoint : $"{mountPoint}/{rest}";
    }

    /// <summary>Zamienia `/p/ai` na `P:\ai`; sciezke spoza znanych punktow montowania zostawia bez zmian</summary>
    public static string ToWindows(string linuxPath, IReadOnlyDictionary<char, string>? mountPoints = null)
    {
        if (string.IsNullOrWhiteSpace(linuxPath) || !linuxPath.StartsWith('/'))
            return linuxPath;

        foreach (var (driveLetter, mountPoint) in _OrderedMountPoints(mountPoints))
        {
            if (!_IsUnderMountPoint(linuxPath, mountPoint))
                continue;

            var rest = linuxPath[mountPoint.Length..].TrimStart('/').Replace('/', '\\');
            return $"{driveLetter}:\\{rest}";
        }

        return linuxPath;
    }

    // Punkt montowania podany przez uzytkownika; porownanie liter jest odporne na wielkosc znakow.
    private static string? _Lookup(IReadOnlyDictionary<char, string>? mountPoints, char driveLetter)
    {
        if (mountPoints is null)
            return null;

        foreach (var entry in mountPoints)
        {
            if (char.ToUpperInvariant(entry.Key) == char.ToUpperInvariant(driveLetter))
                return _NormalizeMountPoint(entry.Value);
        }

        return null;
    }

    // Wszystkie znane przypisania, od najdluzszego punktu montowania, zeby `/mnt/p` wygral z `/mnt`.
    private static IEnumerable<(char DriveLetter, string MountPoint)> _OrderedMountPoints(
        IReadOnlyDictionary<char, string>? mountPoints)
    {
        var map = new Dictionary<char, string>();
        for (var driveLetter = 'A'; driveLetter <= 'Z'; driveLetter++)
            map[driveLetter] = DefaultMountPoint(driveLetter);

        if (mountPoints is not null)
        {
            foreach (var entry in mountPoints)
                map[char.ToUpperInvariant(entry.Key)] = _NormalizeMountPoint(entry.Value);
        }

        return map
            .Select(entry => (entry.Key, entry.Value))
            .OrderByDescending(entry => entry.Value.Length);
    }

    // Dopasowanie musi konczyc sie na granicy katalogu, zeby `/home` nie trafilo pod punkt `/h`.
    private static bool _IsUnderMountPoint(string linuxPath, string mountPoint)
    {
        if (!linuxPath.StartsWith(mountPoint, StringComparison.Ordinal))
            return false;

        return linuxPath.Length == mountPoint.Length || linuxPath[mountPoint.Length] == '/';
    }

    private static string _NormalizeMountPoint(string value)
    {
        var trimmed = value.Trim().Replace('\\', '/').TrimEnd('/');
        if (trimmed.Length == 0)
            return "/";

        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }
}
