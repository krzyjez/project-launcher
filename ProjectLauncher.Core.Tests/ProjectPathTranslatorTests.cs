using Xunit;

namespace ProjectLauncher.Core.Tests;

public class ProjectPathTranslatorTests
{
    // Domyslna konwencja: litera dysku staje sie katalogiem w korzeniu systemu.
    [Theory]
    [InlineData(@"P:\ai", "/p/ai")]
    [InlineData(@"P:\project-launcher\ProjectLauncher.Wpf", "/p/project-launcher/ProjectLauncher.Wpf")]
    [InlineData(@"C:\Users\Krzysztof\ai-tools", "/c/Users/Krzysztof/ai-tools")]
    [InlineData(@"P:\", "/p")]
    public void ToLinux_translates_drive_letter_to_mount_point(string windowsPath, string expected)
    {
        Assert.Equal(expected, ProjectPathTranslator.ToLinux(windowsPath));
    }

    [Theory]
    [InlineData("/p/ai", @"P:\ai")]
    [InlineData("/c/Users/Krzysztof/ai-tools", @"C:\Users\Krzysztof\ai-tools")]
    [InlineData("/p", @"P:\")]
    public void ToWindows_translates_mount_point_back_to_drive_letter(string linuxPath, string expected)
    {
        Assert.Equal(expected, ProjectPathTranslator.ToWindows(linuxPath));
    }

    [Fact]
    public void Custom_mount_point_wins_over_convention()
    {
        var mountPoints = new Dictionary<char, string> { ['P'] = "/projekty" };

        Assert.Equal("/projekty/ai", ProjectPathTranslator.ToLinux(@"P:\ai", mountPoints));
        Assert.Equal(@"P:\ai", ProjectPathTranslator.ToWindows("/projekty/ai", mountPoints));
    }

    [Fact]
    public void Longer_mount_point_wins_over_shorter_one()
    {
        var mountPoints = new Dictionary<char, string> { ['P'] = "/mnt/p" };

        Assert.Equal(@"P:\ai", ProjectPathTranslator.ToWindows("/mnt/p/ai", mountPoints));
    }

    // Katalog domowy nie moze trafic pod punkt montowania dysku H.
    [Fact]
    public void Path_outside_known_mount_points_stays_unchanged()
    {
        Assert.Equal("/home/krzysztof", ProjectPathTranslator.ToWindows("/home/krzysztof"));
        Assert.Equal("/etc/fstab", ProjectPathTranslator.ToWindows("/etc/fstab"));
    }

    [Theory]
    [InlineData(@"\\serwer\udzial\projekt")]
    [InlineData(@"..\obok")]
    [InlineData("")]
    public void Path_without_drive_letter_stays_unchanged(string path)
    {
        Assert.Equal(path, ProjectPathTranslator.ToLinux(path));
    }

    [Fact]
    public void Translation_is_idempotent_for_already_translated_path()
    {
        Assert.Equal("/p/ai", ProjectPathTranslator.ToLinux("/p/ai"));
        Assert.Equal(@"P:\ai", ProjectPathTranslator.ToWindows(@"P:\ai"));
    }

    [Theory]
    [InlineData(@"P:\ai")]
    [InlineData(@"P:\project-launcher")]
    [InlineData(@"C:\Users\Krzysztof\ai-tools")]
    public void Round_trip_returns_original_registry_path(string windowsPath)
    {
        var linuxPath = ProjectPathTranslator.ToLinux(windowsPath);

        Assert.Equal(windowsPath, ProjectPathTranslator.ToWindows(linuxPath));
    }

    [Fact]
    public void Drive_letter_case_does_not_matter_for_custom_mount_points()
    {
        var mountPoints = new Dictionary<char, string> { ['p'] = "projekty" };

        Assert.Equal("/projekty/ai", ProjectPathTranslator.ToLinux(@"P:\ai", mountPoints));
    }
}
