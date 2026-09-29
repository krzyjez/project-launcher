using System.Text.Json.Serialization;

namespace ProjectLauncher.Core;

public sealed class ProjectLauncherSettings
{
    [JsonPropertyName("sortMode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProjectSortMode SortMode { get; set; } = ProjectSortMode.LastLaunched;

    [JsonPropertyName("showDetails")]
    public bool ShowDetails { get; set; }

    // Aktywny projekt bez uruchomienia przez tyle dni sam przechodzi do uspionych; 0 wylacza usypianie.
    [JsonPropertyName("autoSleepAfterDays")]
    public int AutoSleepAfterDays { get; set; } = 90;}
