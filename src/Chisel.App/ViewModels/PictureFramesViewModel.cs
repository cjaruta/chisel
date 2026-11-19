using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Chisel.App.Services;
using Chisel.Core.Pictures;

namespace Chisel.App.ViewModels;

// Every picture frame's runtime state, one PictureFrameViewModel per frame instance, and what they share: the clock, whether
// the window is minimized, and the layout (where the lists live as part of its working copy). MainViewModel.PictureFrames.
public sealed class PictureFramesViewModel : ViewModelBase
{
    private readonly LayoutViewModel _layout;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, PictureFrameViewModel> _frames = new();
    private readonly PictureImportService? _import;
    private DateTimeOffset _noticeAt;

    // How long a message stays up.
    public static readonly TimeSpan NoticeTime = TimeSpan.FromSeconds(6);

    // shuffleSeed: the same seed gives the same shuffle (tests); by default each start shuffles differently.
    public PictureFramesViewModel(LayoutViewModel layout, TimeProvider? time = null, int? shuffleSeed = null, PictureImportService? import = null)
    {
        _layout = layout;
        _import = import;
        _time = time ?? TimeProvider.System;
        ShuffleSeed = shuffleSeed ?? Environment.TickCount;
        _layout.PropertyChanged += OnLayoutChanged;
    }

    public int ShuffleSeed { get; }

    public DateTimeOffset Now => _time.GetUtcNow();

    public bool IsEditing => _layout.IsEditing;

    // Set by the window when it is minimized or restored.
    public bool WindowMinimized { get; set; }

    public FrameContent Content(string frameId) => _layout.Pictures.Get(frameId);

    public PictureFrameViewModel For(string frameId)
    {
        if (!_frames.TryGetValue(frameId, out var frame))
        {
            frame = new PictureFrameViewModel(frameId, this);
            _frames[frameId] = frame;
        }
        return frame;
    }

    public IReadOnlyCollection<PictureFrameViewModel> All => _frames.Values;

    // A short message about a drop (a picture that failed, or that only some were added); null when there is none.
    private string? _notice;

    public string? Notice
    {
        get => _notice;
        private set
        {
            if (SetProperty(ref _notice, value) && value is not null)
            {
                _noticeAt = Now;
            }
        }
    }

    // How many new frames a drop of this many pictures makes (see MultiDropPolicy); at least 1.
    public int FramesFor(int sourceCount) =>
        Math.Max(1, MultiDropPolicy.Plan(_import?.MultiDrop ?? MultiDropMode.OneSlideshow, sourceCount).Frames);

    // Fills the frames a drop landed on, one picture at a time: each is read or downloaded and checked off the UI thread,
    // then stored and added to its frame. A frame the drop made shows as loading until its pictures arrive; one that ends
    // up with none goes away again. Failures set Notice.
    public async Task DropAsync(PictureDropPlacement placement, IReadOnlyList<PictureSource> sources)
    {
        if (_import is null || sources.Count == 0)
        {
            return;
        }
        var mode = _import.MultiDrop;
        var keepOriginals = _import.KeepOriginals;
        var plan = MultiDropPolicy.Plan(mode, sources.Count);

        // Which source goes to which frame: a dropped-into frame takes everything, a new slideshow too; one frame per picture takes the first few.
        var jobs = new List<(PictureSource Source, string Frame)>();
        for (var i = 0; i < sources.Count; i++)
        {
            if (!placement.CreatedFrames || mode == MultiDropMode.OneSlideshow)
            {
                jobs.Add((sources[i], placement.FrameIds[0]));
            }
            else if (i < placement.FrameIds.Count && i < plan.PicturesUsed)
            {
                jobs.Add((sources[i], placement.FrameIds[i]));
            }
        }
        var notices = new List<string>();
        if (placement.CreatedFrames && plan.Notice is not null && sources.Count > jobs.Count)
        {
            notices.Add(plan.Notice);
        }

        foreach (var (_, frameId) in jobs)
        {
            For(frameId).Pending++;
        }
        var failures = new List<string>();
        foreach (var (source, frameId) in jobs)
        {
            PreparedSource prepared;
            try
            {
                prepared = await _import.PrepareAsync(source, keepOriginals);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                prepared = new PreparedSource(null, "That picture can't be read.", "picture");
            }

            if (prepared.Picture is { } picture)
            {
                try
                {
                    var asset = _import.Commit(picture);
                    // A frame cancelled or deleted while the picture was on its way gets nothing (the file is swept later).
                    if (_layout.Layout.WidgetIds.Contains(frameId))
                    {
                        _layout.UpdatePictures(p => p.AddPictures(frameId, [asset.Id]));
                    }
                }
                catch (Exception ex) when (ex is IOException or Microsoft.EntityFrameworkCore.DbUpdateException)
                {
                    failures.Add($"{prepared.Name}: couldn't be saved.");
                }
            }
            else
            {
                failures.Add($"{prepared.Name}: {prepared.Error}");
            }
            For(frameId).Pending--;
        }

        if (placement.CreatedFrames)
        {
            foreach (var frameId in placement.FrameIds.Where(id => For(id).Pending == 0 && _layout.Pictures.Get(id).Pictures.Count == 0))
            {
                _layout.DiscardFrame(frameId);
                Forget(frameId);
            }
        }
        if (failures.Count > 0)
        {
            notices.Add(failures.Count == 1 ? failures[0] : $"{failures.Count} pictures couldn't be added. {failures[0]}");
        }
        if (notices.Count > 0)
        {
            Notice = string.Join(" ", notices);
        }
    }

    // Called about every 200 ms by MainViewModel.Tick.
    public void Tick()
    {
        if (_notice is not null && Now - _noticeAt >= NoticeTime)
        {
            Notice = null;
        }
        foreach (var frame in _frames.Values)
        {
            frame.Tick();
        }
    }

    // Manual mode remembers where the frame was (the layout saves it unless it is being edited).
    internal void SaveCurrentIndex(string frameId, int index)
    {
        var options = Content(frameId).Options;
        if (options.CurrentIndex != index && options.Mode == SlideshowMode.Manual)
        {
            _layout.UpdatePictures(p => p.SetOptions(frameId, p.Get(frameId).Options with { CurrentIndex = index }));
        }
    }

    // A frame whose instance was deleted lets its state go.
    public void Forget(string frameId) => _frames.Remove(frameId);

    private void OnLayoutChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayoutViewModel.Pictures))
        {
            foreach (var frame in _frames.Values)
            {
                frame.ContentChanged();
            }
        }
    }
}
