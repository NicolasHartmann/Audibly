namespace Audibly.Models;

/// <summary>
///     Where an audiobook stands while it is listened to: the values that change during playback.
///     Saving only these keeps saving cheap for books with hundreds of files and chapters.
/// </summary>
public sealed record AudiobookPlaybackState(
    Guid AudiobookId,
    int CurrentSourceFileIndex,
    int CurrentTimeMs,
    int? CurrentChapterIndex,
    double Progress,
    bool IsCompleted,
    DateTime? DateLastPlayed,
    double PlaybackSpeed,
    double Volume,
    bool IsNowPlaying);
