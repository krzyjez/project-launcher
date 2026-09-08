using System.Diagnostics;
using System.IO;

namespace ProjectLauncher.Core;

public static class EditorLauncher
{
    /// <summary>Znajduje Visual Studio Code w typowych lokalizacjach Windows albo przez PATH.</summary>
    public static string? ResolveEditorPath()
    {
        foreach (var candidate in _GetEditorCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return _FindOnPath("code.cmd") ?? _FindOnPath("code.exe");
    }

    /// <summary>Tworzy parametry startowe otwierajace projekt w osobnym oknie VS Code.</summary>
    public static ProcessStartInfo CreateStartInfo(string editorPath, string projectPath)
    {
        return new ProcessStartInfo
        {
            FileName = editorPath,
            Arguments = $"--new-window \"{projectPath}\"",
            UseShellExecute = true
        };
    }

    // Zwraca najczestsze lokalizacje instalacji VS Code na Windows.
    private static IEnumerable<string> _GetEditorCandidates()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            yield return Path.Combine(localAppData, "Programs", "Microsoft VS Code", "Code.exe");
            yield return Path.Combine(localAppData, "Programs", "Microsoft VS Code", "bin", "code.cmd");
        }

        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(programFiles, "Microsoft VS Code", "Code.exe");
            yield return Path.Combine(programFiles, "Microsoft VS Code", "bin", "code.cmd");
        }

        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            yield return Path.Combine(programFilesX86, "Microsoft VS Code", "Code.exe");
            yield return Path.Combine(programFilesX86, "Microsoft VS Code", "bin", "code.cmd");
        }
    }

    // Szuka programu w katalogach z PATH bez uruchamiania shella.
    private static string? _FindOnPath(string fileName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
