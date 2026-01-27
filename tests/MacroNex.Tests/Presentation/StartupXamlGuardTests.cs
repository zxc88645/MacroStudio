using System.Text.RegularExpressions;
using Xunit;

namespace MacroNex.Tests.Presentation;

public class StartupXamlGuardTests
{
    // These tests intentionally do NOT load WPF runtime.
    // They statically scan XAML to prevent common startup-crash patterns.

    [Fact]
    public void MainWindow_ShouldNotUseDynamicResourceAsBindingConverter()
    {
        var xaml = ReadRepoFile(@"src\MacroNex.Presentation\MainWindow.xaml");
        Assert.DoesNotMatch(new Regex(@"Converter=\{DynamicResource\s+", RegexOptions.IgnoreCase), xaml);
    }

    [Fact]
    public void MainWindow_TextBox_ShouldNotTwoWayBind_ToTimeSpanTotalSeconds()
    {
        var xaml = ReadRepoFile(@"src\MacroNex.Presentation\MainWindow.xaml");

        // Typical crash: TextBox Text defaults to TwoWay and TotalSeconds is read-only.
        // We forbid binding paths that include ".TotalSeconds" on TextBox Text unless Mode is explicitly OneWay/OneTime.
        // This is a conservative rule that matches the failures we saw.
        var pattern = new Regex(
            @"<TextBox[^>]*\sText=""\{Binding\s+[^}]*TotalSeconds[^}]*\}""[^>]*/?>",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        foreach (Match m in pattern.Matches(xaml))
        {
            var tag = m.Value;
            var hasSafeMode = Regex.IsMatch(tag, @"Mode\s*=\s*(OneWay|OneTime)", RegexOptions.IgnoreCase);
            Assert.True(hasSafeMode, $"Unsafe TextBox Text binding to TotalSeconds without Mode=OneWay/OneTime:\n{tag}");
        }
    }

    [Fact]
    public void MainWindow_ShouldNotSetWindowBackgroundUsingPropertyElementWithDynamicResourceText()
    {
        var xaml = ReadRepoFile(@"src\MacroNex.Presentation\MainWindow.xaml");
        // Another crash we hit: <Window.Background> {DynamicResource Bg0} </Window.Background>
        Assert.DoesNotMatch(new Regex(@"<Window\.Background>\s*\{DynamicResource\s+", RegexOptions.IgnoreCase), xaml);
    }

    [Fact]
    public void Views_ShouldNotUseDynamicResourceAsBindingConverter()
    {
        var paths = new[]
        {
            @"src\MacroNex.Presentation\Views\ScriptListView.xaml",
            @"src\MacroNex.Presentation\Views\CommandGridView.xaml",
            @"src\MacroNex.Presentation\Views\CountdownWindow.xaml",
            @"src\MacroNex.Presentation\Views\InputDialog.xaml",
            @"src\MacroNex.Presentation\Views\PickPointWindow.xaml",
        };

        foreach (var p in paths)
        {
            var xaml = ReadRepoFile(p);
            Assert.DoesNotMatch(new Regex(@"Converter=\{DynamicResource\s+", RegexOptions.IgnoreCase), xaml);
        }
    }

    private static string ReadRepoFile(string relativeWindowsPath)
    {
        var repoRoot = FindRepoRoot();
        var fullPath = Path.Combine(repoRoot, relativeWindowsPath);
        Assert.True(File.Exists(fullPath), $"Missing expected file: {fullPath}");
        return File.ReadAllText(fullPath);
    }

    private static string FindRepoRoot()
    {
        // When running tests, current directory is typically bin/{config}/{tfm}.
        // Walk up until we find a stable repo marker.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "tasks.md")) &&
                File.Exists(Path.Combine(dir.FullName, "MacroNex.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        // Fallback: current directory
        return Directory.GetCurrentDirectory();
    }
}

