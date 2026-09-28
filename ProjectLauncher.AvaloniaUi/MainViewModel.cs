using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ProjectLauncher.Core;

namespace ProjectLauncher.AvaloniaUi;

/// <summary>Dane widoku glownego okna; w Avalonii okno nie moze samo pelnic tej roli, bo juz zglasza wlasne zmiany</summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private bool _showDetails;

    /// <summary>Numer wersji pokazywany pod naglowkiem</summary>
    public string LauncherVersion => "v" + (typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0");

    public ObservableCollection<ProjectItem> ActiveProjects { get; } = [];

    public ObservableCollection<ProjectItem> ShelvedProjects { get; } = [];

    public ObservableCollection<TagFilterItem> TagFilters { get; } = [];

    /// <summary>Widok szczegolowy pokazuje sciezke, pelny opis i imiona agentow</summary>
    public bool ShowDetails
    {
        get => _showDetails;
        set
        {
            if (_showDetails == value)
                return;

            _showDetails = value;
            _OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void _OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
