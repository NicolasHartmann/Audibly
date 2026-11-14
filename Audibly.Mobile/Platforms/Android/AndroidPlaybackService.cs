using Android.Content;
using AndroidX.Core.Content;
using AndroidX.Media3.Common;
using AndroidX.Media3.Session;
using Audibly.Mobile.Services;
using Java.Lang;
using MediaController = AndroidX.Media3.Session.MediaController;

namespace Audibly.Mobile;

/// <summary>Talks to <see cref="PlaybackService" /> through a Media3 controller.</summary>
public sealed class AndroidPlaybackService : Java.Lang.Object, IPlaybackService, IPlayerListener
{
    // Player.STATE_IDLE and STATE_ENDED in Media3; the binding does not expose them under usable names
    private const int StateIdle = 1;
    private const int StateEnded = 4;

    private readonly Context _context = Android.App.Application.Context;
    private MediaController? _controller;
    private Task<MediaController>? _connecting;

    public bool IsPlaying => _controller?.IsPlaying ?? false;
    public bool PlayWhenReady => _controller?.PlayWhenReady ?? false;
    public bool HasEnded => _controller?.PlaybackState == StateEnded;
    public int SourceIndex => _controller?.CurrentMediaItemIndex ?? 0;
    public double PositionMs => _controller?.CurrentPosition ?? 0;

    public event Action? StateChanged;
    public event Action? Ended;
    public event Action<string>? Failed;

    public async Task OpenAsync(IReadOnlyList<PlaybackItem> items, int sourceIndex, double positionMs, double speed,
        bool play)
    {
        var controller = await ConnectAsync();
        var mediaItems = new List<MediaItem>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var metadata = new MediaMetadata.Builder()!
                .SetTitle(item.Title)!
                .SetArtist(item.Author)!;
            if (!string.IsNullOrEmpty(item.CoverPath) && File.Exists(item.CoverPath))
                metadata.SetArtworkUri(Android.Net.Uri.FromFile(new Java.IO.File(item.CoverPath)));
            mediaItems.Add(new MediaItem.Builder()!
                .SetMediaId(index.ToString())!
                .SetUri(item.Location)!
                .SetMediaMetadata(metadata.Build())!
                .Build()!);
        }

        controller.SetMediaItems(mediaItems, System.Math.Clamp(sourceIndex, 0, System.Math.Max(0, items.Count - 1)),
            (long)System.Math.Max(0, positionMs));
        controller.SetPlaybackSpeed((float)speed);
        controller.Prepare();
        if (play) controller.Play();
    }

    public void Play()
    {
        if (_controller is not { } controller) return;
        // after a failed file the player is idle and ignores Play until it is prepared again
        if (controller.PlaybackState == StateIdle) controller.Prepare();
        controller.Play();
    }
    public void Pause() => _controller?.Pause();
    public void Seek(int sourceIndex, double positionMs) =>
        _controller?.SeekTo(sourceIndex, (long)System.Math.Max(0, positionMs));
    public void SetSpeed(double speed) => _controller?.SetPlaybackSpeed((float)speed);

    public void Stop()
    {
        _controller?.Stop();
        _controller?.ClearMediaItems();
    }

    private Task<MediaController> ConnectAsync()
    {
        if (_controller != null) return Task.FromResult(_controller);
        if (_connecting != null) return _connecting;

        var completion = new TaskCompletionSource<MediaController>();
        var token = new SessionToken(_context,
            new ComponentName(_context, Class.FromType(typeof(PlaybackService))));
        var future = new MediaController.Builder(_context, token).BuildAsync()!;
        future.AddListener(new Runnable(() =>
        {
            try
            {
                _controller = (MediaController)future.Get()!;
                _controller.AddListener(this);
                completion.TrySetResult(_controller);
            }
            catch (System.Exception e)
            {
                _connecting = null;
                completion.TrySetException(e);
            }
        }), ContextCompat.GetMainExecutor(_context));
        return _connecting = completion.Task;
    }

    // IPlayerListener: only the callbacks the app reacts to do anything

    void IPlayerListener.OnIsPlayingChanged(bool isPlaying) => StateChanged?.Invoke();

    void IPlayerListener.OnMediaItemTransition(MediaItem? mediaItem, int reason) => StateChanged?.Invoke();

    void IPlayerListener.OnPlaybackStateChanged(int playbackState)
    {
        if (playbackState == StateEnded) Ended?.Invoke();
        StateChanged?.Invoke();
    }

    void IPlayerListener.OnPlayerError(PlaybackException? error) =>
        Failed?.Invoke(error?.Message ?? "The file could not be played.");
}
