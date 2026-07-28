using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace ProjectLauncher.Wpf;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const double PreferredWindowHeight = 720;
    private const double WindowScreenMargin = 48;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ObservableCollection<ProjectItem> _projects = [];
    private Point _dragStartPoint;
    private bool _suppressNextClick;
    private string? _screenshotPath;
    private ProjectSortMode _sortMode = ProjectSortMode.LastLaunched;
    private bool _showDetails;

    public ObservableCollection<ProjectItem> ActiveProjects { get; } = [];
    public ObservableCollection<ProjectItem> ShelvedProjects { get; } = [];
    public ObservableCollection<TagFilterItem> TagFilters { get; } = [];
    public string LauncherVersion => typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "1.1.0";

    public bool ShowDetails
    {
        get => _showDetails;
        set
        {
            if (_showDetails == value)
                return;

            _showDetails = value;
            _OnPropertyChanged();
            _OnPropertyChanged(nameof(ProjectDetailsVisibility));
        }
    }

    public Visibility ProjectDetailsVisibility => ShowDetails
        ? Visibility.Visible
        : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        _ApplyWindowHeightLimit();
        DataContext = this;
        LoadSettings();
        LoadProjects();
        UpdateSortButtons();
        RebuildTagFilters();
        RebuildProjectLists();
        ReadScreenshotArgument();
    }

    private string ProjectsFilePath => ProjectLauncherPaths.GetProjectsFilePath();

    private string SettingsFilePath => ProjectLauncherPaths.GetSettingsFilePath();

    private void LoadSettings()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath, Encoding.UTF8);
            var settings = JsonSerializer.Deserialize<ProjectLauncherSettings>(json, _jsonOptions);
            _sortMode = settings?.SortMode is ProjectSortMode.LaunchCount or ProjectSortMode.Name
                ? settings.SortMode
                : ProjectSortMode.LastLaunched;
            ShowDetails = settings?.ShowDetails ?? false;
        }
        catch (JsonException)
        {
            _sortMode = ProjectSortMode.LastLaunched;
            ShowDetails = false;
        }
        catch (IOException)
        {
            _sortMode = ProjectSortMode.LastLaunched;
            ShowDetails = false;
        }
    }

    private void SaveSettings()
    {
        var settings = new ProjectLauncherSettings
        {
            SortMode = _sortMode,
            ShowDetails = ShowDetails
        };
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(SettingsFilePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    // Utrzymuje okno w granicach ekranu, a nadmiar projektow oddaje do przewijanej listy.
    private void _ApplyWindowHeightLimit()
    {
        var availableHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - WindowScreenMargin);
        Height = Math.Min(PreferredWindowHeight, availableHeight);
        MaxHeight = availableHeight;
    }

    // Odswieza bindowania ustawien widoku glownego.
    private void _OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void ReadScreenshotArgument()
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--screenshot", StringComparison.OrdinalIgnoreCase))
            {
                _screenshotPath = args[i + 1];
                return;
            }
        }
    }

    private void LoadProjects()
    {
        var json = File.ReadAllText(ProjectsFilePath, Encoding.UTF8);
        var projects = JsonSerializer.Deserialize<List<ProjectItem>>(json, _jsonOptions) ?? [];
        _projects.Clear();

        for (var index = 0; index < projects.Count; index++)
        {
            var project = projects[index];
            if (project.Order <= 0)
            {
                project.Order = index + 1;
            }

            if (string.IsNullOrWhiteSpace(project.Color))
            {
                project.Color = ProjectItem.DefaultColors[index % ProjectItem.DefaultColors.Length];
            }

            if (project.LegacyHidden == true)
            {
                project.Shelved = true;
                project.LegacyHidden = null;
            }

            _projects.Add(project);
        }

        NormalizeProjectOrder();
    }

    private void SaveProjects()
    {
        NormalizeProjectOrder();
        var json = JsonSerializer.Serialize(_projects, _jsonOptions);
        File.WriteAllText(ProjectsFilePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void RebuildProjectLists()
    {
        ActiveProjects.Clear();
        ShelvedProjects.Clear();
        var number = 1;

        foreach (var project in OrderedProjects())
        {
            if (!_ProjectMatchesTagFilter(project))
            {
                continue;
            }

            if (project.Shelved)
            {
                ShelvedProjects.Add(project);
                continue;
            }

            project.Number = number++;
            ActiveProjects.Add(project);
        }

        ShelvedProjectsPanel.Visibility = ShelvedProjects.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    // Odtwarza panel tagow po zmianie danych projektow, zachowujac aktywne filtry.
    private void RebuildTagFilters()
    {
        var selectedTags = TagFilters
            .Where(filter => filter.IsSelected)
            .Select(filter => filter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tagNames = _projects
            .SelectMany(project => project.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        TagFilters.Clear();
        foreach (var tagName in tagNames)
        {
            TagFilters.Add(new TagFilterItem(tagName, _TagFilterSelectionChanged)
            {
                IsSelected = selectedTags.Contains(tagName)
            });
        }

    }

    // Sprawdza filtr typu "lub": wystarczy jeden wspolny tag projektu i filtra.
    private bool _ProjectMatchesTagFilter(ProjectItem project)
    {
        var selectedTags = TagFilters
            .Where(filter => filter.IsSelected)
            .Select(filter => filter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return selectedTags.Count == 0 || project.Tags.Any(selectedTags.Contains);
    }

    // Odswieza obie listy po przelaczeniu przycisku tagu.
    private void _TagFilterSelectionChanged()
    {
        RebuildProjectLists();
    }

    private void NormalizeProjectOrder()
    {
        var order = 1;
        var orderedProjects = _projects.OrderBy(project => project.Order).ToList();
        _projects.Clear();

        foreach (var project in orderedProjects)
        {
            project.Order = order++;
            _projects.Add(project);
        }
    }

    private void OpenProject(ProjectItem project)
    {
        var editorPath = EditorLauncher.ResolveEditorPath();
        if (editorPath is null)
        {
            MessageBox.Show(this, "Nie znaleziono Visual Studio Code.", "Projekty", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (!Directory.Exists(project.Path))
        {
            MessageBox.Show(this, $"Nie znaleziono projektu:\n{project.Path}", "Projekty", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        project.LaunchCount++;
        project.LastLaunched = DateTime.Now.ToString("yyyy-MM-dd");
        SaveProjects();
        WorkspaceColorSettings.Apply(project.Path, project.Color);

        Process.Start(EditorLauncher.CreateStartInfo(editorPath, project.Path));

        Close();
    }

    private void AddProjectButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Wybierz katalog projektu",
            Multiselect = false
        };

        if (Directory.Exists(@"P:\"))
        {
            dialog.InitialDirectory = @"P:\";
        }

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var projectPath = Path.GetFullPath(dialog.FolderName);
        if (_projects.Any(project => SameProjectPath(project.Path, projectPath)))
        {
            MessageBox.Show(this, $"Ten katalog jest juz w rejestrze:\n{projectPath}", "Projekty", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var project = CreateProjectFromPath(projectPath);
        var editor = new EditDescriptionWindow(project, isNewProject: true)
        {
            Owner = this
        };

        if (editor.ShowDialog() != true)
        {
            return;
        }

        project.Name = editor.ProjectName;
        project.Color = editor.ProjectColor;
        project.AgentNames = editor.AgentNames;
        project.Tags = editor.Tags;
        project.Description = editor.DescriptionText;

        _projects.Add(project);
        NormalizeProjectOrder();
        RebuildTagFilters();
        RebuildProjectLists();
        SaveProjects();
    }

    private ProjectItem CreateProjectFromPath(string projectPath)
    {
        return new ProjectItem
        {
            Order = _projects.Count == 0 ? 1 : _projects.Max(project => project.Order) + 1,
            Name = CreateUniqueProjectName(new DirectoryInfo(projectPath).Name),
            Path = projectPath,
            Description = "",
            Color = ChooseAvailableProjectColor(),
            AgentNames = [],
            Tags = [],
            LastLaunched = "",
            LaunchCount = 0,
            Shelved = false
        };
    }

    private string CreateUniqueProjectName(string baseName)
    {
        var name = string.IsNullOrWhiteSpace(baseName) ? "Nowy projekt" : baseName.Trim();
        if (_projects.All(project => !string.Equals(project.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return name;
        }

        for (var suffix = 2; suffix < 1000; suffix++)
        {
            var candidate = $"{name} {suffix}";
            if (_projects.All(project => !string.Equals(project.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        return name;
    }

    private string ChooseAvailableProjectColor()
    {
        var existingColors = _projects
            .Select(project => NormalizeColorForComparison(project.Color))
            .Where(color => color is not null)
            .Select(color => color!)
            .ToList();

        if (existingColors.Count == 0)
        {
            return ProjectItem.DefaultColors[0];
        }

        return ProjectItem.ColorCandidates
            .OrderByDescending(color => MinColorDistance(color, existingColors))
            .ThenBy(color => existingColors.Contains(color, StringComparer.OrdinalIgnoreCase))
            .First();
    }

    private static double MinColorDistance(string candidate, IEnumerable<string> existingColors)
    {
        return existingColors.Min(existingColor => ColorDistance(candidate, existingColor));
    }

    private static double ColorDistance(string first, string second)
    {
        var firstColor = (Color)ColorConverter.ConvertFromString(first);
        var secondColor = (Color)ColorConverter.ConvertFromString(second);
        var red = firstColor.R - secondColor.R;
        var green = firstColor.G - secondColor.G;
        var blue = firstColor.B - secondColor.B;

        return (red * red) + (green * green) + (blue * blue);
    }

    private static string? NormalizeColorForComparison(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().StartsWith('#')
            ? value.Trim().ToUpperInvariant()
            : $"#{value.Trim().ToUpperInvariant()}";

        try
        {
            _ = (Color)ColorConverter.ConvertFromString(normalized);
            return normalized.Length == 7 ? normalized : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool SameProjectPath(string first, string second)
    {
        static string NormalizePath(string path)
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        return string.Equals(NormalizePath(first), NormalizePath(second), StringComparison.OrdinalIgnoreCase);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            return;
        }

        var number = KeyToNumber(e.Key);
        if (number is not null && number.Value >= 1 && number.Value <= ActiveProjects.Count)
        {
            OpenProject(ActiveProjects[number.Value - 1]);
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_screenshotPath))
        {
            return;
        }

        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        WindowScreenshot.SaveToPng(this, _screenshotPath);
        Close();
    }

    private static int? KeyToNumber(Key key)
    {
        if (key >= Key.D1 && key <= Key.D9)
        {
            return key - Key.D0;
        }

        if (key >= Key.NumPad1 && key <= Key.NumPad9)
        {
            return key - Key.NumPad0;
        }

        return null;
    }

    private void ProjectCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (FindProjectItem(sender) is { } project)
        {
            if (_suppressNextClick)
            {
                _suppressNextClick = false;
                return;
            }

            OpenProject(project);
        }
    }

    private void ProjectsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void ProjectsList_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPosition = e.GetPosition(null);
        if (Math.Abs(currentPosition.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource) is { DataContext: ProjectItem project })
        {
            _suppressNextClick = true;
            DragDrop.DoDragDrop(ProjectsList, project, DragDropEffects.Move);
        }
    }

    private void ProjectsList_Drop(object sender, DragEventArgs e)
    {
        if (_sortMode != ProjectSortMode.Order)
        {
            return;
        }

        if (!e.Data.GetDataPresent(typeof(ProjectItem)))
        {
            return;
        }

        var source = (ProjectItem)e.Data.GetData(typeof(ProjectItem))!;
        var target = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource)?.DataContext as ProjectItem;
        if (target is null || ReferenceEquals(source, target))
        {
            return;
        }

        var sourceIndex = _projects.IndexOf(source);
        var targetIndex = _projects.IndexOf(target);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        _projects.RemoveAt(sourceIndex);
        targetIndex = _projects.IndexOf(target);
        _projects.Insert(targetIndex, source);

        NormalizeProjectOrder();
        RebuildProjectLists();
        SaveProjects();
    }

    private void ShelveProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (FindProjectItem(sender) is not { } project)
        {
            return;
        }

        project.Shelved = true;
        RebuildProjectLists();
        SaveProjects();
    }

    private void RestoreProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RestoreProject(FindProjectItem(sender));
    }

    private void DeleteProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DeleteProject(FindProjectItem(sender));
    }

    private void RestoreProject(ProjectItem? project)
    {
        if (project is null)
        {
            return;
        }

        project.Shelved = false;
        RebuildProjectLists();
        SaveProjects();
    }

    private void DeleteProject(ProjectItem? project)
    {
        if (project is null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"Usunac projekt z rejestru?\n\n{project.Name}\n{project.Path}",
            "Projekty",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _projects.Remove(project);
        NormalizeProjectOrder();
        RebuildProjectLists();
        SaveProjects();
    }

    // Wybiera tryb sortowania wskazany przez przycisk w naglowku.
    private void SortModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string value } ||
            !Enum.TryParse<ProjectSortMode>(value, out var sortMode) ||
            sortMode == ProjectSortMode.Order)
        {
            return;
        }

        _sortMode = sortMode;
        UpdateSortButtons();
        SaveSettings();
        RebuildProjectLists();
    }

    // Odswieza podswietlenie aktywnego przycisku sortowania.
    private void UpdateSortButtons()
    {
        SortByLaunchCountButton.IsChecked = _sortMode == ProjectSortMode.LaunchCount;
        SortByLastLaunchedButton.IsChecked = _sortMode == ProjectSortMode.LastLaunched;
        SortByNameButton.IsChecked = _sortMode == ProjectSortMode.Name;
    }

    // Zapisuje preferencje widoku szczegolowego od razu po przelaczeniu.
    private void _DetailsToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ShowDetails = DetailsToggleButton.IsChecked == true;
        SaveSettings();
    }

    // Przelacza pojedynczy tag bez zapisywania tymczasowego filtra do ustawien.
    private void TagFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TagFilterItem filter })
        {
            filter.IsSelected = !filter.IsSelected;
        }
    }

    private void EditDescriptionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (FindProjectItem(sender) is not { } project)
        {
            return;
        }

        var dialog = new EditDescriptionWindow(project)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            project.Name = dialog.ProjectName;
            project.Color = dialog.ProjectColor;
            project.AgentNames = dialog.AgentNames;
            project.Tags = dialog.Tags;
            project.Description = dialog.DescriptionText;
            SaveProjects();
            RebuildTagFilters();
            RebuildProjectLists();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private IEnumerable<ProjectItem> OrderedProjects()
    {
        return _sortMode switch
        {
            ProjectSortMode.LaunchCount => _projects
                .OrderByDescending(project => project.LaunchCount)
                .ThenByDescending(project => project.LastLaunched)
                .ThenBy(project => project.Order),
            ProjectSortMode.Name => _projects
                .OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(project => project.Order),
            _ => _projects
                .OrderByDescending(project => project.LastLaunched)
                .ThenByDescending(project => project.LaunchCount)
                .ThenBy(project => project.Order)
        };
    }

    private static ProjectItem? FindProjectItem(object sender)
    {
        return sender switch
        {
            FrameworkElement { DataContext: ProjectItem project } => project,
            MenuItem menuItem => (menuItem.Parent as ContextMenu)?.PlacementTarget is FrameworkElement { DataContext: ProjectItem project }
                ? project
                : null,
            _ => null
        };
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}

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

public enum ProjectSortMode
{
    Order,
    LaunchCount,
    LastLaunched,
    Name
}

public sealed class ProjectLauncherSettings
{
    [JsonPropertyName("sortMode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProjectSortMode SortMode { get; set; } = ProjectSortMode.LastLaunched;

    [JsonPropertyName("showDetails")]
    public bool ShowDetails { get; set; }
}

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

public sealed class ProjectItem : INotifyPropertyChanged
{
    public static readonly string[] DefaultColors =
    [
        "#FF6B1A",
        "#F2C900",
        "#2FBF8F",
        "#59B9C0",
        "#D9364A",
        "#9B7CFF"
    ];

    public static readonly string[] ColorCandidates =
    [
        "#FF3B30", "#FF6B1A", "#FFCC00", "#B6F000", "#2ECC71", "#16E0A8",
        "#00C2FF", "#3D7BFF", "#7C5CFF", "#B84DFF", "#FF4FD8", "#FF5C8A",
        "#FFFFFF", "#D8D9E6", "#B8B8C8", "#85858F", "#5D6475", "#111318",
        "#C62828", "#AD4B00", "#8D6E00", "#4D7C0F", "#00796B", "#006D9C",
        "#283593", "#5B21B6", "#86198F", "#BE185D", "#7F1D1D", "#3F3F46"
    ];

    private int _number;
    private int _order;
    private string _name = "";
    private string _path = "";
    private string _description = "";
    private string _color = "";
    private List<string> _agentNames = [];
    private List<string> _tags = [];
    private string _lastLaunched = "";
    private int _launchCount;
    private bool _shelved;
    private bool? _legacyHidden;

    [JsonIgnore]
    public int Number
    {
        get => _number;
        set => SetField(ref _number, value);
    }

    [JsonPropertyName("order")]
    public int Order
    {
        get => _order;
        set => SetField(ref _order, value);
    }

    [JsonPropertyName("name")]
    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    [JsonPropertyName("path")]
    public string Path
    {
        get => _path;
        set => SetField(ref _path, value);
    }

    [JsonPropertyName("description")]
    public string Description
    {
        get => _description;
        set
        {
            if (SetField(ref _description, value))
            {
                OnPropertyChanged(nameof(DescriptionVisibility));
            }
        }
    }

    [JsonPropertyName("color")]
    public string Color
    {
        get => _color;
        set
        {
            if (SetField(ref _color, NormalizeColor(value)))
            {
                OnPropertyChanged(nameof(ProjectBrush));
                OnPropertyChanged(nameof(AgentNamesText));
            }
        }
    }

    [JsonPropertyName("agentNames")]
    public List<string> AgentNames
    {
        get => _agentNames;
        set
        {
            if (SetField(ref _agentNames, value.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).ToList()))
            {
                OnPropertyChanged(nameof(AgentNamesText));
            }
        }
    }

    [JsonPropertyName("tags")]
    public List<string> Tags
    {
        get => _tags;
        set => SetField(ref _tags, value.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    [JsonPropertyName("lastLaunched")]
    public string LastLaunched
    {
        get => _lastLaunched;
        set
        {
            if (SetField(ref _lastLaunched, value))
            {
                OnPropertyChanged(nameof(LaunchInfo));
            }
        }
    }

    [JsonPropertyName("launchCount")]
    public int LaunchCount
    {
        get => _launchCount;
        set
        {
            if (SetField(ref _launchCount, value))
            {
                OnPropertyChanged(nameof(LaunchInfo));
            }
        }
    }

    [JsonPropertyName("shelved")]
    public bool Shelved
    {
        get => _shelved;
        set => SetField(ref _shelved, value);
    }

    [JsonPropertyName("hidden")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LegacyHidden
    {
        get => _legacyHidden;
        set => _legacyHidden = value;
    }

    [JsonIgnore]
    public Brush ProjectBrush => CreateBrush(Color);

    [JsonIgnore]
    public string AgentNamesText => AgentNames.Count == 0
        ? "Agenci: domyslna pula"
        : $"Agenci: {string.Join(", ", AgentNames)}";

    [JsonIgnore]
    public string LaunchInfo => string.IsNullOrWhiteSpace(LastLaunchedDate)
        ? $"{LaunchCount} ur."
        : $"{LaunchCount} ur. - {LastLaunchedDate}";

    [JsonIgnore]
    public Visibility DescriptionVisibility => string.IsNullOrWhiteSpace(Description)
        ? Visibility.Collapsed
        : Visibility.Visible;

    private string LastLaunchedDate => LastLaunched.Length >= 10
        ? LastLaunched[..10]
        : LastLaunched;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static Brush CreateBrush(string value)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(NormalizeColor(value)));
        }
        catch (FormatException)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(DefaultColors[0]));
        }
    }

    private static string NormalizeColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultColors[0];
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith('#') ? trimmed.ToUpperInvariant() : $"#{trimmed.ToUpperInvariant()}";
    }
}

public sealed class TagFilterItem : INotifyPropertyChanged
{
    private readonly Action _selectionChanged;
    private bool _isSelected;

    public TagFilterItem(string name, Action selectionChanged)
    {
        Name = name;
        _selectionChanged = selectionChanged;
    }

    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            _selectionChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
