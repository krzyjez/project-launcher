using System.IO;

namespace ProjectLauncher.Core;

public static class ProjectLauncherPaths
{
    public static string GetProjectsFilePath()
    {
        var targetDirectory = GetAiToolsDirectory();
        Directory.CreateDirectory(targetDirectory);

        var targetFile = Path.Combine(targetDirectory, "launch-projects.json");
        if (File.Exists(targetFile))
        {
            return targetFile;
        }

        var legacyFile = FindLegacyProjectsFile();
        if (legacyFile is not null)
        {
            File.Copy(legacyFile, targetFile, overwrite: false);
            return targetFile;
        }

        return targetFile;
    }

    public static string GetSettingsFilePath()
    {
        var targetDirectory = GetAiToolsDirectory();
        Directory.CreateDirectory(targetDirectory);

        return Path.Combine(targetDirectory, "project-launcher-settings.json");
    }

    private static string GetAiToolsDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable("AI_TOOLS_HOME");
        return string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ai-tools")
            : Path.GetFullPath(overridePath);
    }

    private static string? FindLegacyProjectsFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "projects.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
