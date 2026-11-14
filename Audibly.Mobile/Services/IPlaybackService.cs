namespace Audibly.Mobile.Services;

/// <summary>One file of the book that is handed to the platform's player.</summary>
public sealed record PlaybackItem(string Location, string Title, string Author, string? CoverPath);

/// <summary>
///     The platform's audio player, reduced to what the app needs. Positions are audio time in
///     milliseconds within the file at <see cref="SourceIndex" />.
/// </summary>
public interface IPlaybackService
{
    bool IsPlaying { get; }
    int SourceIndex { get; }
    double PositionMs { get; }

    /// <summary>Raised on the main thread when playing, pausing or the current file changes.</summary>
    event Action? StateChanged;

    /// <summary>Raised on the main thread when the last file has played to its end.</summary>
    event Action? Ended;

    /// <summary>Raised on the main thread when a file cannot be played.</summary>
    event Action<string>? Failed;

    Task OpenAsync(IReadOnlyList<PlaybackItem> items, int sourceIndex, double positionMs, double speed, bool play);
    void Play();
    void Pause();
    void Seek(int sourceIndex, double positionMs);
    void SetSpeed(double speed);
    void Stop();
}
