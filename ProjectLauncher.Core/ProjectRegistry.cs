using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ProjectLauncher.Core;

/// <summary>Odczyt i zapis wspolnego rejestru projektow oraz ustawien launchera</summary>
public static class ProjectRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Wczytuje projekty z rejestru, uzupelnia brakujace pola i porzadkuje kolejnosc</summary>
    public static List<ProjectItem> LoadProjects()
    {
        var path = ProjectLauncherPaths.GetProjectsFilePath();

        // Pierwsze uruchomienie na nowym systemie: rejestr jeszcze nie istnieje.
        if (!File.Exists(path))
            return [];

        var json = File.ReadAllText(path, Encoding.UTF8);
        var projects = JsonSerializer.Deserialize<List<ProjectItem>>(json, JsonOptions) ?? [];

        for (var index = 0; index < projects.Count; index++)
        {
            var project = projects[index];
            if (project.Order <= 0)
                project.Order = index + 1;

            if (string.IsNullOrWhiteSpace(project.Color))
                project.Color = ProjectItem.DefaultColors[index % ProjectItem.DefaultColors.Length];

            if (project.LegacyHidden == true)
            {
                project.Shelved = true;
                project.LegacyHidden = null;
            }
        }

        NormalizeOrder(projects);

        return projects;
    }

    /// <summary>Zapisuje projekty do rejestru, wczesniej porzadkujac kolejnosc</summary>
    public static void SaveProjects(IList<ProjectItem> projects)
    {
        NormalizeOrder(projects);
        var json = JsonSerializer.Serialize(projects, JsonOptions);
        File.WriteAllText(ProjectLauncherPaths.GetProjectsFilePath(), json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>Nadaje polu `order` kolejne numery zgodnie z biezaca kolejnoscia recznego sortowania</summary>
    public static void NormalizeOrder(IList<ProjectItem> projects)
    {
        var order = 1;
        var orderedProjects = projects.OrderBy(project => project.Order).ToList();
        projects.Clear();

        foreach (var project in orderedProjects)
        {
            project.Order = order++;
            projects.Add(project);
        }
    }

    /// <summary>Usypia aktywne projekty, ktore nie byly uruchamiane ani przenoszone miedzy kategoriami przez `afterDays` dni; zwraca liczbe uspionych</summary>
    public static int ApplyAutoSleep(IEnumerable<ProjectItem> projects, int afterDays, DateOnly today)
    {
        if (afterDays <= 0)
            return 0;

        var sleptCount = 0;
        foreach (var project in projects.Where(project => project.Status == ProjectStatus.Active))
        {
            // Liczy sie pozniejsza z dat: reczne przywrocenie do aktualnych daje projektowi nowy okres.
            var lastActivity = new[] { _ParseDate(project.LastLaunched), _ParseDate(project.StatusChanged) }.Max();
            if (lastActivity is null || today.DayNumber - lastActivity.Value.DayNumber < afterDays)
                continue;

            project.Status = ProjectStatus.Shelved;
            project.StatusChanged = today.ToString("yyyy-MM-dd");
            sleptCount++;
        }

        return sleptCount;
    }

    // Daty w rejestrze maja format `yyyy-MM-dd`; brak albo bledny zapis oznacza brak daty.
    private static DateOnly? _ParseDate(string value)
    {
        return value.Length >= 10 && DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", out var date) ? date : null;
    }

    /// <summary>Zwraca projekty w kolejnosci wybranego trybu sortowania</summary>
    public static IEnumerable<ProjectItem> Ordered(IEnumerable<ProjectItem> projects, ProjectSortMode sortMode)
    {
        return sortMode switch
        {
            ProjectSortMode.LaunchCount => projects
                .OrderByDescending(project => project.LaunchCount)
                .ThenByDescending(project => project.LastLaunched)
                .ThenBy(project => project.Order),
            ProjectSortMode.Name => projects
                .OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(project => project.Order),
            _ => projects
                .OrderByDescending(project => project.LastLaunched)
                .ThenByDescending(project => project.LaunchCount)
                .ThenBy(project => project.Order)
        };
    }

    /// <summary>Wczytuje ustawienia widoku; przy braku albo bledzie pliku zwraca wartosci domyslne</summary>
    public static ProjectLauncherSettings LoadSettings()
    {
        var path = ProjectLauncherPaths.GetSettingsFilePath();
        if (!File.Exists(path))
            return new ProjectLauncherSettings();

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var settings = JsonSerializer.Deserialize<ProjectLauncherSettings>(json, JsonOptions);
            if (settings is null)
                return new ProjectLauncherSettings();

            // Kolejnosc reczna nie jest trybem wybieralnym w naglowku.
            if (settings.SortMode is not (ProjectSortMode.LaunchCount or ProjectSortMode.Name))
                settings.SortMode = ProjectSortMode.LastLaunched;

            return settings;
        }
        catch (JsonException)
        {
            return new ProjectLauncherSettings();
        }
        catch (IOException)
        {
            return new ProjectLauncherSettings();
        }
    }

    /// <summary>Zapisuje ustawienia widoku launchera</summary>
    public static void SaveSettings(ProjectLauncherSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(ProjectLauncherPaths.GetSettingsFilePath(), json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
