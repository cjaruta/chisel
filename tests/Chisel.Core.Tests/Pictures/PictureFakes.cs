using Chisel.Core.Pictures;

namespace Chisel.Core.Tests.Pictures;

internal sealed class FakeRepository : IPictureRepository
{
    public List<PictureAsset> Stored { get; } = [];
    public PictureFrameSet Frames { get; private set; } = PictureFrameSet.Empty;
    public PictureFrameSet LoadFrames() => Frames;
    public void SaveFrames(PictureFrameSet set) => Frames = set;
    public IReadOnlyList<PictureAsset> Assets() => Stored;
    public PictureAsset? Find(string hash) => Stored.FirstOrDefault(a => a.Hash == hash);
    public PictureAsset? Get(Guid id) => Stored.FirstOrDefault(a => a.Id == id);
    public void AddAsset(PictureAsset asset) => Stored.Add(asset);
    public void DeleteAssets(IEnumerable<Guid> ids) => Stored.RemoveAll(a => ids.Contains(a.Id));
}

internal sealed class FakeStore : IPictureFileStore
{
    public Dictionary<string, byte[]> Files { get; } = [];
    public int Writes { get; private set; }
    public string PathOf(string storedName) => "pictures/" + storedName;
    public bool Exists(string storedName) => Files.ContainsKey(storedName);
    public void Write(string storedName, byte[] bytes) { Files[storedName] = bytes; Writes++; }
    public void Delete(string storedName) => Files.Remove(storedName);
}
