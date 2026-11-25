using Chisel.Core.Clipboard;

namespace Chisel.Core.Tests.Clipboard;

public class ClipboardFilterTests
{
    private static readonly ClipboardHistorySettings Settings = ClipboardHistorySettings.Default;

    private static ClipboardSkip Check(string? text, ClipboardFlags flags = ClipboardFlags.None, string? owner = null) =>
        ClipboardFilter.Check(new ClipboardSnapshot(text, flags, owner), Settings);

    [Theory]
    [InlineData("4111111111111111", true)]       // a common test card number
    [InlineData("4111 1111 1111 1111", true)]
    [InlineData("378282246310005", true)]        // 15 digits
    [InlineData("4111111111111112", false)]      // fails the check digit
    [InlineData("411111111111", false)]          // 12 digits: too short to be a card
    [InlineData("41111111111111111111", false)]  // 20 digits: too long
    [InlineData("4111-1111-1111-111a", false)]
    public void Luhn_AcceptsOnlyValidCardLengthNumbers(string text, bool valid) =>
        Assert.Equal(valid, Luhn.IsCardNumber(text));

    [Theory]
    [InlineData(ClipboardFlags.ExcludeFromMonitoring)]
    [InlineData(ClipboardFlags.NoHistory)]
    [InlineData(ClipboardFlags.NoCloudUpload)]
    [InlineData(ClipboardFlags.ViewerIgnore)]
    public void ExclusionFormats_AreSkipped(ClipboardFlags flags) =>
        Assert.Equal(ClipboardSkip.Excluded, Check("plain words", flags));

    [Theory]
    [InlineData("KeePass")]
    [InlineData("keepassxc")]
    [InlineData("1Password")]
    [InlineData("Bitwarden")]
    public void PasswordManagers_AreSkipped(string owner) =>
        Assert.Equal(ClipboardSkip.PasswordManager, Check("plain words", owner: owner));

    [Fact]
    public void NoText_AndBlankText_AreSkipped()
    {
        Assert.Equal(ClipboardSkip.NoText, Check(null));
        Assert.Equal(ClipboardSkip.Empty, Check("  \r\n "));
    }

    [Fact]
    public void TextOverTheLengthLimit_IsSkipped()
    {
        var limit = Settings.MaxLength;
        Assert.Equal(ClipboardSkip.None, Check(new string('a', limit)));
        Assert.Equal(ClipboardSkip.TooLong, Check(new string('a', limit + 1)));
        var tight = Settings with { MaxLength = 5 };
        Assert.Equal(ClipboardSkip.TooLong, ClipboardFilter.Check(new ClipboardSnapshot("abcdef"), tight));
    }

    [Theory]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("card: 4111-1111-1111-1111 exp 01/30")]
    [InlineData("aZ3kP9qL2mX7vB4nR8tY1wC6")]
    [InlineData("ghp_abcdefghijklmnopqrstuvwxyz0123456789")]
    [InlineData("sk-test0000000000000000000000")]
    [InlineData("AKIAABCDEFGHIJKLMNOP")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dBjftJeZ4CVPmB92K27uhbUJU1p1r_wW1gFWFOEjXk")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----\nMIIB\n-----END RSA PRIVATE KEY-----")]
    public void SecretLookingText_IsSkipped(string text) =>
        Assert.Equal(ClipboardSkip.LooksLikeSecret, Check(text));

    [Theory]
    [InlineData("hello world")]
    [InlineData("Meeting moved to 3pm on Friday")]
    [InlineData("4111 1111 1111 1112")]
    [InlineData("supercalifragilisticexpialidocious")]
    [InlineData("https://example.com/articles/2026/some-long-article-title?id=12345")]
    [InlineData(@"C:\Users\Test\Documents\report-2026-final.docx")]
    [InlineData("user@example.com")]
    [InlineData("Call 555-0100 ext 42")]
    public void OrdinaryText_IsKept(string text) =>
        Assert.Equal(ClipboardSkip.None, Check(text));

    [Fact]
    public void Snapshot_ToString_NeverShowsTheText()
    {
        var snapshot = new ClipboardSnapshot("private words", ClipboardFlags.None, "app");
        Assert.DoesNotContain("private", snapshot.ToString());
    }
}
