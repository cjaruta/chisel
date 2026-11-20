using Chisel.Core.Pictures;

namespace Chisel.Core.Tests.Pictures;

public class PictureRulesTests
{
    // ----- slideshow

    [Theory]
    [InlineData(0, 3, 1)]
    [InlineData(2, 3, 0)]
    [InlineData(0, 1, 0)]
    public void Next_WrapsAround(int index, int count, int expected) => Assert.Equal(expected, Slideshow.Next(index, count));

    [Theory]
    [InlineData(0, 3, 2)]
    [InlineData(2, 3, 1)]
    [InlineData(0, 1, 0)]
    public void Previous_WrapsAround(int index, int count, int expected) => Assert.Equal(expected, Slideshow.Previous(index, count));

    [Fact]
    public void PlayOrder_InOrder_IsTheListOrder_AndShuffleIsAPermutationThatIsTheSameForASeed()
    {
        Assert.Equal(new[] { 0, 1, 2, 3 }, Slideshow.PlayOrder(4, SlideshowOrder.InOrder, 7));

        var first = Slideshow.PlayOrder(20, SlideshowOrder.Shuffle, 42);
        var again = Slideshow.PlayOrder(20, SlideshowOrder.Shuffle, 42);
        var other = Slideshow.PlayOrder(20, SlideshowOrder.Shuffle, 43);
        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
        Assert.Equal(Enumerable.Range(0, 20), first.OrderBy(i => i));
        Assert.NotEqual(Enumerable.Range(0, 20), first);
    }

    [Fact]
    public void PlayOrder_OfNothing_IsEmpty() => Assert.Empty(Slideshow.PlayOrder(0, SlideshowOrder.Shuffle, 1));

    [Fact]
    public void ClickHalf_RightGoesForward_LeftGoesBack()
    {
        Assert.Equal(1, Slideshow.StepForClick(300, 400));
        Assert.Equal(-1, Slideshow.StepForClick(100, 400));
        Assert.Equal(1, Slideshow.StepForClick(200, 400));
    }

    [Fact]
    public void ADrag_IsNotAClick_OnlyAfterTheThreshold()
    {
        Assert.True(Slideshow.IsClick(2, 1));
        Assert.False(Slideshow.IsClick(6, 0));
        Assert.False(Slideshow.IsClick(4, 4));
    }

    // ----- interval

    [Theory]
    [InlineData("30s", 30)]
    [InlineData("30 sec", 30)]
    [InlineData("5 min", 300)]
    [InlineData("5m", 300)]
    [InlineData("1h", 3600)]
    [InlineData("2 hours", 7200)]
    [InlineData("45", 45)]
    [InlineData("1.5 min", 90)]
    public void Interval_Parse_ReadsANumberAndAUnit(string text, int seconds) =>
        Assert.Equal(seconds, SlideshowInterval.TryParse(text));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5 min")]
    [InlineData("5 parsecs")]
    [InlineData("0")]
    public void Interval_Parse_RefusesNonsense(string text) => Assert.Null(SlideshowInterval.TryParse(text));

    [Fact]
    public void Interval_IsKeptBetweenFiveSecondsAndADay()
    {
        Assert.Equal(5, SlideshowInterval.Clamp(1));
        Assert.Equal(86400, SlideshowInterval.Clamp(999999));
        Assert.Equal(90, SlideshowInterval.FromCustom(1.5, IntervalUnit.Minutes));
    }

    [Fact]
    public void Interval_HasThePresets_AndFormatsThemBack()
    {
        Assert.Equal(new[] { 10, 30, 60, 300, 600, 900, 1800, 3600 }, SlideshowInterval.Presets);
        Assert.Equal("10 s", SlideshowInterval.Format(10));
        Assert.Equal("1 min", SlideshowInterval.Format(60));
        Assert.Equal("15 min", SlideshowInterval.Format(900));
        Assert.Equal("1 h", SlideshowInterval.Format(3600));
        Assert.Equal("90 s", SlideshowInterval.Format(90));
    }

    // ----- storage and drops

    [Fact]
    public void KeepOriginals_KeepsTheFile_WhenOff_ShrinksOnlyBigStillPictures()
    {
        Assert.False(PictureStoragePolicy.Decide(true, 6000, 4000, false).Resize);

        var big = PictureStoragePolicy.Decide(false, 6000, 4000, false);
        Assert.True(big.Resize);
        Assert.Equal(2048, big.Width);
        Assert.Equal(1365, big.Height);

        var tall = PictureStoragePolicy.Decide(false, 3000, 6000, false);
        Assert.Equal((1024, 2048), (tall.Width, tall.Height));

        Assert.False(PictureStoragePolicy.Decide(false, 1600, 900, false).Resize);
        Assert.False(PictureStoragePolicy.Decide(false, 6000, 4000, true).Resize);
    }

    [Fact]
    public void MultiDrop_OneSlideshow_MakesOneFrameWithEveryPicture()
    {
        var plan = MultiDropPolicy.Plan(MultiDropMode.OneSlideshow, 12);
        Assert.Equal(1, plan.Frames);
        Assert.Equal(12, plan.PicturesUsed);
        Assert.Null(plan.Notice);
    }

    [Fact]
    public void MultiDrop_OneFramePerPicture_StopsAtEight_AndSaysSo()
    {
        var few = MultiDropPolicy.Plan(MultiDropMode.OneFramePerPicture, 3);
        Assert.Equal((3, 3), (few.Frames, few.PicturesUsed));
        Assert.Null(few.Notice);

        var many = MultiDropPolicy.Plan(MultiDropMode.OneFramePerPicture, 11);
        Assert.Equal((8, 8), (many.Frames, many.PicturesUsed));
        Assert.Contains("8", many.Notice);

        Assert.Equal(1, MultiDropPolicy.Plan(MultiDropMode.OneFramePerPicture, 1).Frames);
        Assert.Equal(0, MultiDropPolicy.Plan(MultiDropMode.OneSlideshow, 0).Frames);
    }

    // ----- urls

    [Theory]
    [InlineData("https://example.com/a.png", true)]
    [InlineData("http://example.com/a.png", true)]
    [InlineData("data:image/png;base64,AAAA", true)]
    [InlineData("data:text/html;base64,AAAA", false)]
    [InlineData("data:image/svg+xml;base64,AAAA", false)]
    [InlineData("file:///c:/a.png", false)]
    [InlineData("ftp://example.com/a.png", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("not a url", false)]
    public void PictureUrls_AllowOnlyHttpHttpsAndDataImages(string url, bool allowed) =>
        Assert.Equal(allowed, PictureUrls.IsAllowed(url));

    [Fact]
    public void ImageUrlFromHtml_FindsTheFirstImgSrc()
    {
        Assert.Equal("https://example.com/a.jpg", PictureUrls.FromHtml("<a href='x'><img alt=\"t\" src=\"https://example.com/a.jpg\" width=3></a>"));
        Assert.Equal("https://example.com/b.png", PictureUrls.FromHtml("<IMG SRC='https://example.com/b.png'>"));
        Assert.Null(PictureUrls.FromHtml("<p>no picture</p>"));
        Assert.Null(PictureUrls.FromHtml(""));
    }

    // ----- sweep

    [Fact]
    public void Sweep_ListsOnlyAssetsNoFrameUses()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var set = PictureFrameSet.Empty.AddPictures("frame:1", new[] { a }).AddPictures("frame:2", new[] { a, b });

        Assert.Equal(new[] { c }, AssetSweep.Unused(new[] { a, b, c }, set.UsedAssetIds));
        Assert.Empty(AssetSweep.Unused(new[] { a, b }, set.UsedAssetIds));
    }

    // ----- crop

    [Fact]
    public void Crop_FillsTheViewWithTheMiddleOfAWideImage()
    {
        var rect = CropMath.SourceRect(2000, 1000, 500, 500, 0.5, 0.5, 1);
        Assert.Equal(1000, rect.Width, 3);
        Assert.Equal(1000, rect.Height, 3);
        Assert.Equal(500, rect.X, 3);
        Assert.Equal(0, rect.Y, 3);
    }

    [Fact]
    public void Crop_FollowsTheFocalPoint_ButNeverLeavesTheImage()
    {
        var left = CropMath.SourceRect(2000, 1000, 500, 500, 0, 0.5, 1);
        Assert.Equal(0, left.X, 3);
        var right = CropMath.SourceRect(2000, 1000, 500, 500, 1, 0.5, 1);
        Assert.Equal(1000, right.X, 3);
        var zoomed = CropMath.SourceRect(2000, 1000, 500, 500, 1, 1, 4);
        Assert.Equal(250, zoomed.Width, 3);
        Assert.Equal(2000, zoomed.X + zoomed.Width, 3);
        Assert.Equal(1000, zoomed.Y + zoomed.Height, 3);
    }

    [Fact]
    public void Pan_MovesTheFocalPointOppositeTheDrag_AndStaysInRange()
    {
        var (x, y) = CropMath.Pan(2000, 1000, 500, 500, 0.5, 0.5, 1, dragX: 100, dragY: 0);
        Assert.True(x < 0.5);
        Assert.Equal(0.5, y, 3);
        var (edgeX, _) = CropMath.Pan(2000, 1000, 500, 500, 0.1, 0.5, 1, dragX: 5000, dragY: 0);
        Assert.Equal(0, edgeX, 3);
        // An image that already fits exactly across has nothing to pan on that axis.
        var (_, tallY) = CropMath.Pan(1000, 1000, 500, 500, 0.5, 0.5, 1, 0, 80);
        Assert.Equal(0.5, tallY, 3);
    }

    // ----- frame set

    [Fact]
    public void FrameSet_AddsOnceAndKeepsTheOldSetUnchanged()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var before = PictureFrameSet.Empty.AddPictures("f", new[] { a });
        var after = before.AddPictures("f", new[] { a, b });

        Assert.Single(before.Get("f").Pictures);
        Assert.Equal(new[] { a, b }, after.Get("f").Pictures.Select(p => p.AssetId));
        Assert.False(PictureFrameSet.Empty.Has("f"));
    }

    [Fact]
    public void FrameSet_RemovesMovesAndCrops()
    {
        var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        var set = PictureFrameSet.Empty.AddPictures("f", ids);

        Assert.Equal(new[] { ids[1], ids[0], ids[2] }, set.Move("f", 0, 1).Get("f").Pictures.Select(p => p.AssetId));
        Assert.Same(set, set.Move("f", 0, -1));
        Assert.Equal(new[] { ids[0], ids[2] }, set.RemovePicture("f", 1).Get("f").Pictures.Select(p => p.AssetId));

        var cropped = set.SetCrop("f", 2, 2, -1, 99).Get("f").Pictures[2];
        Assert.Equal((1.0, 0.0, FramePicture.MaxZoom), (cropped.FocalX, cropped.FocalY, cropped.Zoom));
    }

    [Fact]
    public void FrameSet_PrunedTo_DropsFramesThatLeftTheDashboard()
    {
        var set = PictureFrameSet.Empty.AddPictures("f1", new[] { Guid.NewGuid() }).AddPictures("f2", new[] { Guid.NewGuid() });

        Assert.Equal(new[] { "f2" }, set.PrunedTo(new[] { "f2", "notes" }).FrameIds);
        Assert.Same(set, set.PrunedTo(new[] { "f1", "f2" }));
    }

    // ----- assets

    private static readonly string Hash = new('a', 64);

    [Fact]
    public void Asset_Create_KeepsAPlainFileName_AndNamesTheStoredFileByHash()
    {
        var asset = PictureAsset.Create(Hash.ToUpperInvariant(), @"C:\Users\someone\My Pic.JPG", ImageFormats.Jpeg, 10, 20, 100, "", DateTimeOffset.UnixEpoch);

        Assert.Equal("My Pic.JPG", asset.FileName);
        Assert.Equal(Hash + ".jpg", asset.StoredName);
        Assert.Null(asset.SourceUrl);
    }

    [Theory]
    [InlineData("short", 1, 1, 1, ImageFormats.Png)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 0, 1, 1, ImageFormats.Png)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 1, 1, 0, ImageFormats.Png)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 1, 1, 1, "image/svg+xml")]
    public void Asset_Create_RefusesBrokenRecords(string hash, int width, int height, long bytes, string mime) =>
        Assert.Throws<ArgumentException>(() => PictureAsset.Create(hash, "a.png", mime, width, height, bytes, null, DateTimeOffset.UnixEpoch));

    [Fact]
    public void Dedupe_ReturnsTheExistingAssetForTheSameBytes()
    {
        var existing = PictureAsset.Create(Hash, "one.png", ImageFormats.Png, 1, 1, 10, null, DateTimeOffset.UnixEpoch);
        var again = PictureAsset.Create(Hash, "two.png", ImageFormats.Png, 1, 1, 10, "https://example.com/two.png", DateTimeOffset.UnixEpoch);
        var other = PictureAsset.Create(new string('b', 64), "three.png", ImageFormats.Png, 1, 1, 10, null, DateTimeOffset.UnixEpoch);

        Assert.Same(existing, PictureAssets.Resolve(new[] { existing }, again));
        Assert.Same(other, PictureAssets.Resolve(new[] { existing }, other));
    }
}
