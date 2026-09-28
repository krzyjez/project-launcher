using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using ProjectLauncher.Core;

namespace ProjectLauncher.Wpf;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const double WindowScreenMargin = 16;

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
        SourceInitialized += (_, _) => _ApplyWindowHeightLimit();
        LocationChanged += (_, _) => _ApplyWindowHeightLimit();
        DpiChanged += (_, _) => _ApplyWindowHeightLimit();
        DataContext = this;
        LoadSettings();
        LoadProjects();
        UpdateSortButtons();
        RebuildTagFilters();
        RebuildProjectLists();
        ReadScreenshotArgument();
    }

    private void LoadSettings()
    {
        var settings = ProjectRegistry.LoadSettings();
        _sortMode = settings.SortMode;
        ShowDetails = settings.ShowDetails;
    }

    private void SaveSettings()
    {
        ProjectRegistry.SaveSettings(new ProjectLauncherSettings
        {
            SortMode = _sortMode,
            ShowDetails = ShowDetails
        });
    }

    // Okno rosnie pod liczbe projektow (SizeToContent), ale nie wyzej niz uzyteczna wysokosc ekranu;
    // dopiero po osiagnieciu tego limitu wlacza sie przewijanie listy.
    // Limit jest przeliczany takze po przeniesieniu okna, bo monitory moga miec rozne rozdzielczosci.
    private void _ApplyWindowHeightLimit()
    {
        var limit = Math.Max(MinHeight, _GetCurrentScreenWorkAreaHeight() - WindowScreenMargin);

        // Bez tego progu zmiana MaxHeight w trakcie przeciagania moglaby sie zapetlac z LocationChanged.
        if (Math.Abs(MaxHeight - limit) < 1)
            return;

        MaxHeight = limit;
    }

    // Wysokosc obszaru roboczego monitora, na ktorym stoi okno, w jednostkach WPF.
    private double _GetCurrentScreenWorkAreaHeight()
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle == IntPtr.Zero)
            return SystemParameters.WorkArea.Height;

        var monitorInfo = new NativeMonitorInfo { Size = Marshal.SizeOf<NativeMonitorInfo>() };
        if (!GetMonitorInfoW(MonitorFromWindow(windowHandle, MonitorDefaultToNearest), ref monitorInfo))
            return SystemParameters.WorkArea.Height;

        var workAreaPixels = monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top;
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleY;

        return scale > 0 ? workAreaPixels / scale : workAreaPixels;
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
        _projects.Clear();
        foreach (var project in ProjectRegistry.LoadProjects())
        {
            _projects.Add(project);
        }
    }

    private void SaveProjects()
    {
        ProjectRegistry.SaveProjects(_projects);
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
        ProjectRegistry.NormalizeOrder(_projects);
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
        var existingProject = _projects.FirstOrDefault(project => SameProjectPath(project.Path, projectPath));
        if (existingProject is not null)
        {
            // Odstawiony projekt nie jest widoczny w glownej liscie, wiec bez tej podpowiedzi
            // komunikat wyglada jak blad rejestru.
            var shelvedHint = existingProject.Shelved ? "\n\nProjekt jest wsrod odstawionych." : "";
            MessageBox.Show(this, $"Ten katalog jest juz w rejestrze:\n{projectPath}{shelvedHint}", "Projekty", MessageBoxButton.OK, MessageBoxImage.Information);
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

    // Okno nie ma belki tytulu, wiec przenoszenie startuje z dowolnego pustego miejsca w oknie.
    private void _Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
            return;

        if (_IsInteractiveElement(e.OriginalSource as DependencyObject))
            return;

        DragMove();
    }

    // Kontrolki i karty projektow obsluguja klikniecie po swojemu, wiec nie moga przenosic okna.
    private static bool _IsInteractiveElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase or ListBoxItem or ScrollBar or Thumb)
                return true;

            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return false;
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
        _ScrollShelvedProjectIntoView(project);
    }

    // Pokazuje odstawiony projekt w dolnej sekcji; bez tego swiezo odstawiony projekt
    // trafia pod widoczny obszar listy i wyglada na zniknietego.
    private void _ScrollShelvedProjectIntoView(ProjectItem project)
    {
        if (!ShelvedProjects.Contains(project))
            return;

        ShelvedProjectsList.UpdateLayout();
        ShelvedProjectsList.ScrollIntoView(project);
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
        return ProjectRegistry.Ordered(_projects, _sortMode);
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

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(IntPtr monitorHandle, ref NativeMonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }
}
