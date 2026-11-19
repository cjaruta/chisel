using System.Text.RegularExpressions;

namespace Chisel.App.Tests.Models;

// Colors belong in the theme layer. This scans the app's XAML and C# for literal colors (hex values, named colors,
// Color.Parse, Colors.* and Brushes.* other than Transparent) outside the few places that are allowed to have them.
public class ThemeGuardTests
{
    // Theme files; the schedule palettes (user-chosen data, not theme); weather gradients and drawing; sample art for captures.
    private static readonly string[] Allowed =
    [
        "Theming/", "Models/BlockPalette.cs", "Views/Widgets/Backdrop/", "Dev/",
    ];

    private static readonly Regex Literal = new(
        @"#[0-9A-Fa-f]{8}\b|#[0-9A-Fa-f]{6}\b|#[0-9A-Fa-f]{3,4}\b|\bColors\.[A-Z]|(?<!Binding )\bBrushes\.(?!Transparent\b)[A-Z]|Color\.(Parse|FromRgb|FromArgb|FromUInt32)|new Color\(|"
        + @"(Foreground|Background|Fill|Stroke|BorderBrush)=""(White|Black|Gray|Grey|Red|Green|Blue|Orange|Yellow|Silver|DimGray|LightGray|DarkGray)""|"
        + @"Value=""(White|Black|Gray|Grey|Red|Green|Blue|Orange|Yellow|Silver)""",
        RegexOptions.Compiled);

    private static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Chisel.App")))
        {
            dir = dir.Parent;
        }
        return Path.Combine(dir!.FullName, "src", "Chisel.App");
    }

    [Fact]
    public void NoLiteralColorsOutsideTheThemeFiles()
    {
        var root = SourceRoot();
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".axaml") || f.EndsWith(".cs")))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.StartsWith("obj/") || relative.StartsWith("bin/") || Allowed.Any(relative.StartsWith))
            {
                continue;
            }
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var text = lines[i];
                if (text.TrimStart().StartsWith("//") || text.TrimStart().StartsWith("<!--"))
                {
                    continue;
                }
                var match = Literal.Match(text);
                if (match.Success)
                {
                    offenders.Add($"{relative}:{i + 1}: {match.Value}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Literal colors found (move them to Theming/):\n" + string.Join("\n", offenders));
    }
}
