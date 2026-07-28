using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Media;

namespace ProjectLauncher.Wpf;

public static class WorkspaceColorSettings
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public static void Apply(string projectPath, string accentHex)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            {
                return;
            }

            var vscodeDirectory = Path.Combine(projectPath, ".vscode");
            var settingsPath = Path.Combine(vscodeDirectory, "settings.json");
            var root = ReadSettings(settingsPath);
            var customizations = GetOrCreateObject(root, "workbench.colorCustomizations");
            var palette = WorkspacePalette.FromAccent(accentHex);

            customizations["titleBar.activeBackground"] = palette.Header;
            customizations["titleBar.activeForeground"] = palette.Foreground;
            customizations["titleBar.inactiveBackground"] = palette.HeaderMuted;
            customizations["titleBar.inactiveForeground"] = palette.ForegroundMuted;
            customizations["activityBar.background"] = palette.ActivityBar;
            customizations["activityBar.foreground"] = palette.Foreground;
            customizations["activityBarBadge.background"] = palette.Accent;
            customizations["activityBarBadge.foreground"] = palette.BadgeForeground;
            customizations["sideBar.background"] = palette.Sidebar;
            customizations["sideBar.foreground"] = palette.Foreground;
            customizations["sideBar.border"] = palette.Accent;
            customizations["statusBar.background"] = palette.StatusBar;
            customizations["statusBar.foreground"] = palette.Foreground;
            customizations["statusBar.noFolderBackground"] = palette.StatusBar;
            customizations["statusBar.debuggingBackground"] = palette.StatusBar;

            Directory.CreateDirectory(vscodeDirectory);
            File.WriteAllText(settingsPath, root.ToJsonString(WriteOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch
        {
            // Opening the project is more important than decorating the workspace.
        }
    }

    private static JsonObject ReadSettings(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return [];
        }

        var json = File.ReadAllText(settingsPath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonNode.Parse(json, documentOptions: ReadOptions) as JsonObject ?? [];
    }

    private static JsonObject GetOrCreateObject(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        root[propertyName] = created;
        return created;
    }

    private sealed record WorkspacePalette(
        string Accent,
        string Header,
        string HeaderMuted,
        string ActivityBar,
        string Sidebar,
        string StatusBar,
        string Foreground,
        string ForegroundMuted,
        string BadgeForeground)
    {
        public static WorkspacePalette FromAccent(string accentHex)
        {
            var accent = ParseColor(accentHex);
            var header = Mix(accent, Colors.Black, 0.55);
            var headerMuted = Mix(accent, Colors.Black, 0.72);
            var activityBar = Mix(accent, Colors.Black, 0.78);
            var sidebar = Mix(accent, Colors.Black, 0.9);
            var statusBar = Mix(accent, Colors.Black, 0.45);
            var foreground = ForegroundFor(header);
            var badgeForeground = ForegroundFor(accent);

            return new WorkspacePalette(
                ToHex(accent),
                ToHex(header),
                ToHex(headerMuted),
                ToHex(activityBar),
                ToHex(sidebar),
                ToHex(statusBar),
                foreground,
                "#D8D1C8",
                badgeForeground);
        }
    }

    private static Color ParseColor(string value)
    {
        try
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "#FF6B1A"
                : value.Trim().StartsWith('#') ? value.Trim() : $"#{value.Trim()}";
            return (Color)ColorConverter.ConvertFromString(normalized);
        }
        catch
        {
            return (Color)ColorConverter.ConvertFromString("#FF6B1A");
        }
    }

    private static Color Mix(Color first, Color second, double secondAmount)
    {
        var firstAmount = 1 - secondAmount;
        return Color.FromRgb(
            (byte)Math.Round(first.R * firstAmount + second.R * secondAmount),
            (byte)Math.Round(first.G * firstAmount + second.G * secondAmount),
            (byte)Math.Round(first.B * firstAmount + second.B * secondAmount));
    }

    private static string ForegroundFor(Color background)
    {
        var luminance = (0.2126 * background.R + 0.7152 * background.G + 0.0722 * background.B) / 255;
        return luminance > 0.56 ? "#101116" : "#FFFFFF";
    }

    private static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
