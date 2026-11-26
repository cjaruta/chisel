using Chisel.Core.Links;
using Xunit;

namespace Chisel.Core.Tests.Links;

public class QuickLinkTargetTests
{
    private sealed class FakeProbe(params string[] existing) : IPathProbe
    {
        public bool DirectoryExists(string path) => existing.Contains(path) && !Path.HasExtension(path);
        public bool FileExists(string path) => existing.Contains(path) && Path.HasExtension(path);
    }

    private static readonly FakeProbe Probe = new(@"C:\Docs", @"C:\Docs\notes.txt", @"C:\Tools\setup.exe", @"C:\Docs\run.ps1", @"\\server\share");

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/a?b=c")]
    [InlineData("  https://example.com  ")]
    public void WebAddresses_AreAccepted(string input)
    {
        var check = QuickLinkTarget.Validate(input, Probe);

        Assert.True(check.IsValid, check.Error);
        Assert.Equal(QuickLinkKind.Web, check.Kind);
        Assert.Equal(input.Trim(), check.Target);
    }

    [Theory]
    [InlineData("ftp://files.example.com")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ms-settings:network")]
    [InlineData("mailto:user@example.com")]
    [InlineData("not a url")]
    [InlineData("example.com")]
    [InlineData("docs\\notes.txt")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void OtherSchemesAndRelativeText_AreRejected(string? input)
    {
        var check = QuickLinkTarget.Validate(input, Probe);

        Assert.False(check.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(check.Error));
    }

    [Fact]
    public void AFolderAndAFileThatExist_AreAccepted_WithTheirKind()
    {
        Assert.Equal(QuickLinkKind.Folder, QuickLinkTarget.Validate(@"C:\Docs", Probe).Kind);
        Assert.Equal(QuickLinkKind.File, QuickLinkTarget.Validate(@"C:\Docs\notes.txt", Probe).Kind);
        Assert.True(QuickLinkTarget.Validate(@"\\server\share", Probe).IsValid);
    }

    [Fact]
    public void QuotesAroundAPastedPath_AreRemoved()
    {
        var check = QuickLinkTarget.Validate("\"C:\\Docs\\notes.txt\"", Probe);

        Assert.True(check.IsValid);
        Assert.Equal(@"C:\Docs\notes.txt", check.Target);
    }

    [Fact]
    public void AMissingPath_IsRejected()
    {
        var check = QuickLinkTarget.Validate(@"C:\Nowhere\file.txt", Probe);

        Assert.False(check.IsValid);
        Assert.Contains("doesn't exist", check.Error);
    }

    [Theory]
    [InlineData(@"C:\Tools\setup.exe")]
    [InlineData(@"C:\Docs\run.ps1")]
    public void ProgramsAndScripts_AreRefusedEvenWhenTheyExist(string path)
    {
        var check = QuickLinkTarget.Validate(path, Probe);

        Assert.False(check.IsValid);
        Assert.Contains("programs and scripts", check.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(@"C:\Tools\SETUP.EXE")]
    [InlineData(@"C:\a\b.bat")]
    [InlineData(@"C:\a\b.cmd")]
    [InlineData(@"C:\a\b.vbs")]
    [InlineData(@"C:\a\b.lnk")]
    [InlineData(@"C:\a\b.msi")]
    public void TheExtensionCheckIsCaseInsensitive_AndNeedsNoDisk(string path)
    {
        Assert.False(QuickLinkTarget.CheckShape(path) is null);
    }

    [Fact]
    public void CheckShape_NeedsNoDisk_AndAcceptsWellFormedWebAndPathText()
    {
        Assert.Null(QuickLinkTarget.CheckShape("https://example.com"));
        Assert.Null(QuickLinkTarget.CheckShape(@"C:\Anything\here.txt"));
    }
}
