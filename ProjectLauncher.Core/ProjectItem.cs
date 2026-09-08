using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ProjectLauncher.Core;

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
                OnPropertyChanged(nameof(DescriptionFirstLine));
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
    public string AgentNamesText => AgentNames.Count == 0
        ? "Agenci: domyslna pula"
        : $"Agenci: {string.Join(", ", AgentNames)}";

    [JsonIgnore]
    public string LaunchInfo => string.IsNullOrWhiteSpace(LastLaunchedDate)
        ? $"{LaunchCount} ur."
        : $"{LaunchCount} ur. - {LastLaunchedDate}";

    // Pierwsza niepusta linia opisu; uzywana w kompaktowej sekcji projektow odstawionych.
    [JsonIgnore]
    public string DescriptionFirstLine => Description
        .Split('\n')
        .Select(line => line.Trim())
        .FirstOrDefault(line => line.Length > 0) ?? "";

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
