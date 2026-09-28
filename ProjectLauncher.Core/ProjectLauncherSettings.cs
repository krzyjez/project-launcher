using System.Text.Json.Serialization;

namespace ProjectLauncher.Core;

public sealed class ProjectLauncherSettings
{
    [JsonPropertyName("sortMode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProjectSortMode SortMode { get; set; } = ProjectSortMode.LastLaunched;

    [JsonPropertyName("showDetails")]
    public bool ShowDetails { get; set; }

    // Opis zadania programu branch pod opisem projektu; domyslnie wlaczony, wylaczany z menu ikony w zasobniku.
    [JsonPropertyName("showTaskDescriptions")]
    public bool ShowTaskDescriptions { get; set; } = true;
}
