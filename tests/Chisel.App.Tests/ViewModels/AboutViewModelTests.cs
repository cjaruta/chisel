using Chisel.App.ViewModels;

namespace Chisel.App.Tests.ViewModels;

public class AboutViewModelTests
{
    [Fact]
    public void DiagnosticInfo_ListsVersionSystemScalingAndWidgetTypes()
    {
        var text = DiagnosticInfo.Build("1.2.3", "Windows 10.0.26100", "10.0.1", scaling: 1.5, screens: 2,
            ["clock", "notes", "notes:2", "countdown:a1b2", "tasks"]);

        Assert.Equal(
            "Chisel 1.2.3\n" +
            "OS: Windows 10.0.26100\n" +
            ".NET: 10.0.1\n" +
            "Display scaling: 150%\n" +
            "Screens: 2\n" +
            "Widgets: clock, countdown, notes x2, tasks\n",
            text.ReplaceLineEndings("\n"));
    }

    // Instance keys, titles and anything else that could name the user's things are left out: only widget types are listed.
    [Fact]
    public void DiagnosticInfo_HasNoInstanceKeysOrPaths()
    {
        var text = DiagnosticInfo.Build("1.2.3", "Windows", "10.0.1", 1, 1, ["notes:Test User diary", @"C:\Users\Test User"]);

        Assert.DoesNotContain("Test User", text);
        Assert.DoesNotContain("diary", text);
    }

    [Fact]
    public void Commands_OpenTheProjectIssuesAndLogs_AndCopyTheInfo()
    {
        var opened = new List<string>();
        var copied = new List<string>();
        var vm = new AboutViewModel("1.2.3", () => "info", opened.Add, () => opened.Add("logs"), text =>
        {
            copied.Add(text);
            return Task.CompletedTask;
        });

        vm.OpenProjectCommand.Execute(null);
        vm.OpenIssuesCommand.Execute(null);
        vm.OpenLogsCommand.Execute(null);
        vm.CopyDiagnosticsCommand.Execute(null);

        Assert.Equal("Chisel 1.2.3", vm.Version);
        Assert.Equal([AboutViewModel.ProjectUrl, AboutViewModel.IssuesUrl, "logs"], opened);
        Assert.Equal(["info"], copied);
        Assert.Equal("Copied. It has no personal data, so it is safe to paste into an issue.", vm.Status);
    }
}
