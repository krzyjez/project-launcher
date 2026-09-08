using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ProjectLauncher.Core;

namespace ProjectLauncher.AvaloniaUi;

public partial class MainWindow : Window
{
    private const double WindowScreenMargin = 16;

    private readonly MainViewModel _model = new();
    private readonly List<ProjectItem> _projects = [];
    private readonly string? _screenshotPath;
    private ProjectSortMode _sortMode;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(string? screenshotPath)
    {
        InitializeComponent();

        _screenshotPath = screenshotPath;
        DataContext = _model;

        var settings = ProjectRegistry.LoadSettings();
        _sortMode = settings.SortMode;
        _model.ShowDetails = settings.ShowDetails;
        DetailsButton.IsChecked = settings.ShowDetails;

        _projects.AddRange(ProjectRegistry.LoadProjects());
        _UpdateSortButtons();
        _RebuildTagFilters();
        _RebuildProjectLists();

        Opened += _OnOpened;
        PositionChanged += (_, _) => _ApplyWindowHeightLimit();
    }

    // Okno rosnie pod liczbe projektow, ale nie wyzej niz uzyteczna wysokosc monitora, na ktorym stoi.
    private void _ApplyWindowHeightLimit()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
            return;

        var limit = Math.Max(MinHeight, screen.WorkingArea.Height / screen.Scaling - WindowScreenMargin);

        // Bez tego progu zmiana limitu w trakcie przeciagania moglaby sie zapetlac ze zdarzeniem pozycji.
        if (Math.Abs(MaxHeight - limit) < 1)
            return;

        MaxHeight = limit;
    }

    private async void _OnOpened(object? sender, EventArgs e)
    {
        _ApplyWindowHeightLimit();

        if (string.IsNullOrWhiteSpace(_screenshotPath))
            return;

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        _SaveScreenshot(_screenshotPath);
        Close();
    }

    private void _SaveScreenshot(string screenshotPath)
    {
        var size = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(Bounds.Width)),
            Math.Max(1, (int)Math.Ceiling(Bounds.Height)));

        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        bitmap.Render(this);

        var directory = Path.GetDirectoryName(screenshotPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        bitmap.Save(screenshotPath);
    }

    // Odtwarza obie listy projektow z uwzglednieniem filtra tagow i biezacego sortowania.
    private void _RebuildProjectLists()
    {
        _model.ActiveProjects.Clear();
        _model.ShelvedProjects.Clear();
        var number = 1;

        foreach (var project in ProjectRegistry.Ordered(_projects, _sortMode))
        {
            if (!_ProjectMatchesTagFilter(project))
                continue;

            if (project.Shelved)
            {
                _model.ShelvedProjects.Add(project);
                continue;
            }

            project.Number = number++;
            _model.ActiveProjects.Add(project);
        }

        ShelvedProjectsPanel.IsVisible = _model.ShelvedProjects.Count > 0;
    }

    private void _RebuildTagFilters()
    {
        var selectedTags = _model.TagFilters
            .Where(filter => filter.IsSelected)
            .Select(filter => filter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var tagNames = _projects
            .SelectMany(project => project.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _model.TagFilters.Clear();
        foreach (var tagName in tagNames)
        {
            _model.TagFilters.Add(new TagFilterItem(tagName, _RebuildProjectLists)
            {
                IsSelected = selectedTags.Contains(tagName)
            });
        }
    }

    // Filtr typu "lub": wystarczy jeden wspolny tag projektu i filtra.
    private bool _ProjectMatchesTagFilter(ProjectItem project)
    {
        var selectedTags = _model.TagFilters
            .Where(filter => filter.IsSelected)
            .Select(filter => filter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return selectedTags.Count == 0 || project.Tags.Any(selectedTags.Contains);
    }

    private void _OpenProject(ProjectItem project)
    {
        var editorPath = EditorLauncher.ResolveEditorPath();
        if (editorPath is null || !Directory.Exists(project.Path))
            return;

        project.LaunchCount++;
        project.LastLaunched = DateTime.Now.ToString("yyyy-MM-dd");
        ProjectRegistry.SaveProjects(_projects);
        WorkspaceColorSettings.Apply(project.Path, project.Color);

        Process.Start(EditorLauncher.CreateStartInfo(editorPath, project.Path));

        Close();
    }

    private void _SaveSettings()
    {
        ProjectRegistry.SaveSettings(new ProjectLauncherSettings
        {
            SortMode = _sortMode,
            ShowDetails = _model.ShowDetails
        });
    }

    private void _UpdateSortButtons()
    {
        SortByLaunchCountButton.IsChecked = _sortMode == ProjectSortMode.LaunchCount;
        SortByLastLaunchedButton.IsChecked = _sortMode == ProjectSortMode.LastLaunched;
        SortByNameButton.IsChecked = _sortMode == ProjectSortMode.Name;
    }

    private void ProjectCard_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;

        if (sender is Control { DataContext: ProjectItem project })
            _OpenProject(project);
    }

    private void ShelveProjectMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_FindProject(sender) is not { } project)
            return;

        project.Shelved = true;
        _RebuildProjectLists();
        ProjectRegistry.SaveProjects(_projects);
    }

    private void RestoreProjectMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_FindProject(sender) is not { } project)
            return;

        project.Shelved = false;
        _RebuildProjectLists();
        ProjectRegistry.SaveProjects(_projects);
    }

    private void TagFilterButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: TagFilterItem filter })
            filter.IsSelected = !filter.IsSelected;
    }

    private void DetailsButton_Click(object? sender, RoutedEventArgs e)
    {
        _model.ShowDetails = DetailsButton.IsChecked == true;
        _SaveSettings();
    }

    private void SortModeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string value } ||
            !Enum.TryParse<ProjectSortMode>(value, out var sortMode) ||
            sortMode == ProjectSortMode.Order)
        {
            return;
        }

        _sortMode = sortMode;
        _UpdateSortButtons();
        _SaveSettings();
        _RebuildProjectLists();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            return;
        }

        var number = _KeyToNumber(e.Key);
        if (number is not null && number.Value >= 1 && number.Value <= _model.ActiveProjects.Count)
            _OpenProject(_model.ActiveProjects[number.Value - 1]);
    }

    // Okno nie ma belki tytulu, wiec przeciaga sie je za dowolne puste miejsce.
    private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (_IsInteractiveSource(e.Source as Visual))
            return;

        BeginMoveDrag(e);
    }

    // Karty i przyciski obsluguja klikniecie same, wiec nie moga przenosic okna.
    private static bool _IsInteractiveSource(Visual? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is Button or ToggleButton or MenuItem or ScrollBar)
                return true;

            if (current is Border border && (border.Classes.Contains("card") || border.Classes.Contains("shelvedRow")))
                return true;

            current = current.GetVisualParent();
        }

        return false;
    }

    private static ProjectItem? _FindProject(object? sender)
    {
        if (sender is not Control control)
            return null;

        if (control.DataContext is ProjectItem project)
            return project;

        return (control.Parent as ContextMenu)?.DataContext as ProjectItem;
    }

    private static int? _KeyToNumber(Key key)
    {
        if (key is >= Key.D1 and <= Key.D9)
            return key - Key.D1 + 1;

        if (key is >= Key.NumPad1 and <= Key.NumPad9)
            return key - Key.NumPad1 + 1;

        return null;
    }
}
