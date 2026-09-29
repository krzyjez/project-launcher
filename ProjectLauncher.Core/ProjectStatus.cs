using System.Text.Json.Serialization;

namespace ProjectLauncher.Core;

/// <summary>Kategoria projektu w rejestrze; zapisywana w polu `status`, brak pola oznacza projekt aktywny</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectStatus>))]
public enum ProjectStatus
{
    /// <summary>Projekt, nad ktorym trwa praca; tylko aktywne dostaja numery 1-9</summary>
    [JsonStringEnumMemberName("active")]
    Active,

    /// <summary>Projekt uspiony: rzadko uzywany, ale jest szansa na powrot</summary>
    [JsonStringEnumMemberName("shelved")]
    Shelved,

    /// <summary>Projekt w archiwum: zamkniety na stale, zostaje w rejestrze tylko do wgladu</summary>
    [JsonStringEnumMemberName("closed")]
    Closed
}
