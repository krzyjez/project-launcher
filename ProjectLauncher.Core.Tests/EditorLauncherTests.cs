using Xunit;

namespace ProjectLauncher.Core.Tests;

public class EditorLauncherTests
{
    // Launcher uruchomiony z terminala VS Code nie moze przekazac edytorowi zmiennych, przez ktore Code.exe nie otwiera okna.
    [Fact]
    public void Start_info_drops_variables_inherited_from_vs_code_terminal()
    {
        Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", "1");
        Environment.SetEnvironmentVariable("VSCODE_IPC_HOOK_CLI", "pipe");
        try
        {
            var startInfo = EditorLauncher.CreateStartInfo(@"C:\Code\Code.exe", @"G:\Mój dysk\Exocortex");

            Assert.False(startInfo.Environment.ContainsKey("ELECTRON_RUN_AS_NODE"));
            Assert.False(startInfo.Environment.ContainsKey("VSCODE_IPC_HOOK_CLI"));
            Assert.True(startInfo.Environment.ContainsKey("PATH"));
            Assert.Equal(["--new-window", @"G:\Mój dysk\Exocortex"], startInfo.ArgumentList);
            Assert.False(startInfo.UseShellExecute);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", null);
            Environment.SetEnvironmentVariable("VSCODE_IPC_HOOK_CLI", null);
        }
    }
}
