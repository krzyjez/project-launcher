using System.Text.Json;
using Xunit;

namespace ProjectLauncher.Core.Tests;

public class ProjectStatusTests
{
    private static readonly DateOnly Today = new(2026, 9, 28);

    [Theory]
    [InlineData("""{"name":"a"}""", ProjectStatus.Active)]
    [InlineData("""{"name":"a","shelved":true}""", ProjectStatus.Shelved)]
    [InlineData("""{"name":"a","status":"closed","shelved":true}""", ProjectStatus.Closed)]
    [InlineData("""{"name":"a","status":"shelved","shelved":true}""", ProjectStatus.Shelved)]
    [InlineData("""{"name":"a","status":"active","shelved":false}""", ProjectStatus.Active)]
    public void Status_is_read_from_new_field_with_fallback_to_legacy_shelved(string json, ProjectStatus expected)
    {
        Assert.Equal(expected, JsonSerializer.Deserialize<ProjectItem>(json)!.Status);
    }

    // Stare narzedzia czytaja tylko `shelved`: uspione i archiwalne musza dla nich wygladac na odstawione.
    [Theory]
    [InlineData(ProjectStatus.Active, "active", false)]
    [InlineData(ProjectStatus.Shelved, "shelved", true)]
    [InlineData(ProjectStatus.Closed, "closed", true)]
    public void Status_is_written_together_with_legacy_shelved(ProjectStatus status, string statusText, bool shelved)
    {
        var json = JsonSerializer.Serialize(new ProjectItem { Name = "a", Status = status });
        using var document = JsonDocument.Parse(json);

        Assert.Equal(statusText, document.RootElement.GetProperty("status").GetString());
        Assert.Equal(shelved, document.RootElement.GetProperty("shelved").GetBoolean());
    }

    [Fact]
    public void Round_trip_keeps_closed_status()
    {
        var json = JsonSerializer.Serialize(new ProjectItem { Name = "a", Status = ProjectStatus.Closed });

        Assert.Equal(ProjectStatus.Closed, JsonSerializer.Deserialize<ProjectItem>(json)!.Status);
    }

    [Fact]
    public void Auto_sleep_moves_only_active_projects_idle_longer_than_limit()
    {
        var idle = Project("2026-06-29");           // 91 dni
        var recent = Project("2026-07-01");         // 89 dni
        var neverLaunched = Project("");
        var closed = Project("2025-01-01", ProjectStatus.Closed);

        var slept = ProjectRegistry.ApplyAutoSleep([idle, recent, neverLaunched, closed], 90, Today);

        Assert.Equal(1, slept);
        Assert.Equal(ProjectStatus.Shelved, idle.Status);
        Assert.Equal("2026-09-28", idle.StatusChanged);
        Assert.Equal(ProjectStatus.Active, recent.Status);
        Assert.Equal(ProjectStatus.Active, neverLaunched.Status);
        Assert.Equal(ProjectStatus.Closed, closed.Status);
    }

    // Recznie przywrocony stary projekt nie moze zasnac przy nastepnym odswiezeniu.
    [Fact]
    public void Recent_manual_restore_protects_old_project_from_auto_sleep()
    {
        var restored = Project("2025-01-01");
        restored.StatusChanged = "2026-09-20";

        Assert.Equal(0, ProjectRegistry.ApplyAutoSleep([restored], 90, Today));
        Assert.Equal(ProjectStatus.Active, restored.Status);
    }

    [Fact]
    public void Zero_days_disables_auto_sleep()
    {
        var idle = Project("2020-01-01");

        Assert.Equal(0, ProjectRegistry.ApplyAutoSleep([idle], 0, Today));
        Assert.Equal(ProjectStatus.Active, idle.Status);
    }

    private static ProjectItem Project(string lastLaunched, ProjectStatus status = ProjectStatus.Active)
    {
        return new ProjectItem { Name = "p", LastLaunched = lastLaunched, Status = status };
    }
}
