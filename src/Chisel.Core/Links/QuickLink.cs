namespace Chisel.Core.Links;

public class QuickLink
{
    public QuickLink(string label, string url) : this(Guid.NewGuid(), label, url)
    {
    }

    public QuickLink(Guid id, string label, string url)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label cannot be empty.", nameof(label));

        // Url is a web address or a full path to a document or folder (see QuickLinkTarget); whether a path exists is
        // checked when the link is made in Settings, not here, so a saved link always loads.
        if (QuickLinkTarget.CheckShape(url) is { } problem)
            throw new ArgumentException(problem, nameof(url));

        Id = id;
        Label = label.Trim();
        Url = QuickLinkTarget.Clean(url);
    }

    public Guid Id { get; }
    public string Label { get; }
    public string Url { get; }
}