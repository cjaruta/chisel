namespace Chisel.Core.Notes;

public class Note
{
    private readonly TimeProvider _time;

    public Note(TimeProvider time, string title, string body)
    {
        _time = time;
        Title = title;
        Body = body;
        UpdatedAt = _time.GetUtcNow();
    }

    public string Title { get; private set; }
    public string Body { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public int WordCount =>
        Body.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    public void EditBody(string newBody)
    {
        Body = newBody;
        UpdatedAt = _time.GetUtcNow();
    }

    public void EditTitle(string newTitle)
    {
        Title = newTitle;
        UpdatedAt = _time.GetUtcNow();
    }
}