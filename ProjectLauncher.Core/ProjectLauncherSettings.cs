using System.Text.Json.Serialization;

namespace ProjectLauncher.Core;

public sealed class ProjectLauncherSettings
{
    [JsonPropertyName("sortMode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProjectSortMode SortMode { get; set; } = ProjectSortMode.LastLaunched;

    [JsonPropertyName("showDetails")]
    public bool ShowDetails { get; set; }}
