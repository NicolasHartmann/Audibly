using System;
using System.Collections.Generic;
using System.Linq;
using Audibly.Models;

namespace Audibly.Core;

/// <summary>
///     Where a position lies in a book that may consist of several files. Positions are audio time
///     in the file's own timeline, as the players report them.
/// </summary>
public static class PlaybackPosition
{
    /// <summary>
    ///     The chapter of a file that contains the position. A boundary belongs to the following
    ///     chapter; a position past the last chapter of the file gives that last chapter.
    /// </summary>
    public static ChapterInfo? ChapterAt(IReadOnlyList<ChapterInfo> chapters, int sourceIndex, double positionMs)
    {
        ChapterInfo? first = null;
        ChapterInfo? match = null;
        foreach (var chapter in chapters.Where(c => c.ParentSourceFileIndex == sourceIndex).OrderBy(c => c.StartTime))
        {
            first ??= chapter;
            if (chapter.StartTime <= positionMs) match = chapter;
        }

        return match ?? first;
    }

    /// <summary>
    ///     The position after skipping forward or back, which may lie in another file. Stops at the
    ///     start and at the end of the book. Null when the book has no files.
    /// </summary>
    public static (int SourceIndex, double PositionMs)? Skip(IReadOnlyList<double> sourceDurationsSeconds,
        int sourceIndex, double positionMs, double deltaSeconds)
    {
        if (sourceDurationsSeconds.Count == 0) return null;

        var totalMs = sourceDurationsSeconds.Sum() * 1000;
        var target = PlayerProgress.BookPositionMs(sourceDurationsSeconds, sourceIndex, positionMs) +
                     deltaSeconds * 1000;
        return PlayerProgress.Locate(sourceDurationsSeconds, Math.Clamp(target, 0, totalMs));
    }

    /// <summary>The chapter before (-1) or after (+1) the current one, or null at the ends of the book.</summary>
    public static ChapterInfo? Neighbor(IReadOnlyList<ChapterInfo> chapters, int currentIndex, int direction) =>
        chapters.FirstOrDefault(chapter => chapter.Index == currentIndex + direction);
}
