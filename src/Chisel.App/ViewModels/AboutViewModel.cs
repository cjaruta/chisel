using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chisel.Core.Layout;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// Settings > Help: the version, links to the project and its issue tracker, the logs folder, and diagnostic info to paste
// into an issue.
public sealed partial class AboutViewModel : ViewModelBase
{
    public const string ProjectUrl = "https://github.com/cjaruta/chisel";
    public const string IssuesUrl = ProjectUrl + "/issues";

    private readonly Func<string> _diagnostics;
    private readonly Action<string> _openUrl;
    private readonly Action _openLogs;
    private readonly Func<string, Task> _copy;

    public AboutViewModel(string version, Func<string> diagnostics, Action<string> openUrl, Action openLogs, Func<string, Task> copy)
    {
        Version = "Chisel " + version;
        _diagnostics = diagnostics;
        _openUrl = openUrl;
        _openLogs = openLogs;
        _copy = copy;
    }

    public string Version { get; }

    [ObservableProperty]
    public partial string Status { get; set; } = "";

    [RelayCommand]
    private void OpenProject() => _openUrl(ProjectUrl);

    [RelayCommand]
    private void OpenIssues() => _openUrl(IssuesUrl);

    [RelayCommand]
    private void OpenLogs() => _openLogs();

    [RelayCommand]
    private async Task CopyDiagnostics()
    {
        await _copy(_diagnostics());
        Status = "Copied. It has no personal data, so it is safe to paste into an issue.";
    }
}

// What helps with a bug report and nothing personal: versions, display scaling, screen count and the widget types in use
// (never instance keys, titles, paths or content).
public static class DiagnosticInfo
{
    public static string Build(string appVersion, string os, string runtime, double scaling, int screens, IEnumerable<string> widgetIds)
    {
        var widgets = widgetIds
            .Select(InstanceIds.TypeOf)
            .Where(type => type.Length > 0 && type.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            .GroupBy(type => type)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key);
        return new StringBuilder()
            .AppendLine($"Chisel {appVersion}")
            .AppendLine($"OS: {os}")
            .AppendLine($".NET: {runtime}")
            .AppendLine($"Display scaling: {scaling * 100:0}%")
            .AppendLine($"Screens: {screens}")
            .AppendLine($"Widgets: {string.Join(", ", widgets)}")
            .ToString();
    }
}
