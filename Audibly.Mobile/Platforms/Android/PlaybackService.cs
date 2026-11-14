using Android.App;
using Android.Content;
using Android.Content.PM;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.Session;

namespace Audibly.Mobile;

/// <summary>
///     Owns the player and its media session, so playback continues with the screen off and after
///     the app's pages are closed. Media3 shows the playback notification for this service itself.
/// </summary>
[Service(Exported = true, ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
[IntentFilter(["androidx.media3.session.MediaSessionService"])]
public class PlaybackService : MediaSessionService
{
    private MediaSession? _session;

    public override void OnCreate()
    {
        base.OnCreate();

        // spoken word: pause for calls and other players, and when headphones are unplugged
        var audio = new AudioAttributes.Builder()!
            .SetUsage(C.UsageMedia)!
            .SetContentType(C.AudioContentTypeSpeech)!
            .Build()!;
        var player = new ExoPlayerBuilder(this)
            .SetAudioAttributes(audio, true)!
            .SetHandleAudioBecomingNoisy(true)!
            // without this the phone may put the CPU to sleep with the screen off and playback stalls
            .SetWakeMode(C.WakeModeLocal)!
            .Build()!;

        var builder = new MediaSession.Builder(this, player);
        // tapping the playback notification opens the app
        if (PackageManager?.GetLaunchIntentForPackage(PackageName!) is { } launch)
            builder.SetSessionActivity(PendingIntent.GetActivity(this, 0, launch,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!);
        _session = builder.Build();
    }

    public override MediaSession? OnGetSession(MediaSession.ControllerInfo? controllerInfo) => _session;

    public override void OnTaskRemoved(Intent? rootIntent)
    {
        // swiping the app away while paused ends the service; while playing it keeps going
        var player = _session?.Player;
        if (player is null || !player.PlayWhenReady || player.MediaItemCount == 0) StopSelf();
    }

    public override void OnDestroy()
    {
        _session?.Player?.Release();
        _session?.Release();
        _session = null;
        base.OnDestroy();
    }
}
