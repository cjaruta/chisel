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

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Url must be a valid http or https address.", nameof(url));

        Id = id;
        Label = label.Trim();
        Url = url;
    }

    public Guid Id { get; }
    public string Label { get; }
    public string Url { get; }
}