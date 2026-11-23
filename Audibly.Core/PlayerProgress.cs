using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Audibly.Core;

/// <summary>What the player's progress bar covers.</summary>
public enum PlayerProgressScope
{
    Chapter = 0,
    Book = 1
}

/// <summary>What the player shows at the end of its progress bar.</summary>
public enum PlayerTimeDisplay
{
    ElapsedAndTotal = 0,
    Remaining = 1,
    Percent = 2
}

/// <summary>
///     Texts and positions for the player's progress bar. Elapsed and total time are audio time;
///     only the remaining time depends on the playback speed.
/// </summary>
public static class PlayerProgress
{
    public static string Elapsed(double positionMs)
    {
        var time = TimeSpan.FromMilliseconds(double.IsFinite(positionMs) ? Math.Max(0, positionMs) : 0);
        return $"{(int)time.TotalHours}:{time:mm}:{time:ss}";
    }

    /// <summary>The text at the end of the bar for a range of <paramref name="durationMs" />.</summary>
    public static string End(PlayerTimeDisplay display, double durationMs, double positionMs, double speed)
    {
        switch (display)
        {
            case PlayerTimeDisplay.Remaining:
                return "-" + PlaybackTimeRemaining.Format(durationMs, positionMs, speed);
            case PlayerTimeDisplay.Percent:
                var valid = double.IsFinite(durationMs) && double.IsFinite(positionMs) && durationMs > 0;
                // rounded down, so 100 % only shows at the very end
                var percent = valid ? Math.Clamp(Math.Floor(positionMs / durationMs * 100), 0, 100) : 0;
                return $"{percent:0} %";
            default:
                return Elapsed(durationMs);
        }
    }

    public static PlayerTimeDisplay Next(PlayerTimeDisplay display) => display switch
    {
        PlayerTimeDisplay.ElapsedAndTotal => PlayerTimeDisplay.Remaining,
        PlayerTimeDisplay.Remaining => PlayerTimeDisplay.Percent,
        _ => PlayerTimeDisplay.ElapsedAndTotal
    };

    public static PlayerProgressScope Other(PlayerProgressScope scope) =>
        scope == PlayerProgressScope.Book ? PlayerProgressScope.Chapter : PlayerProgressScope.Book;

    /// <summary>
    ///     The small line under the progress bar for the scope the bar does not show, e.g.
    ///     "Book  4:00:00 · 10:00:00", "Book  4:00:00 · 3:00:00 left" or "Chapter  50 %".
    /// </summary>
    public static string SecondLine(PlayerProgressScope scope, PlayerTimeDisplay display, double durationMs,
        double positionMs, double speed)
    {
        var label = scope == PlayerProgressScope.Book ? "Book" : "Chapter";
        return display switch
        {
            PlayerTimeDisplay.Percent => $"{label}  {End(display, durationMs, positionMs, speed)}",
            PlayerTimeDisplay.Remaining =>
                $"{label}  {Elapsed(positionMs)} · {PlaybackTimeRemaining.Format(durationMs, positionMs, speed)} left",
            _ => $"{label}  {Elapsed(positionMs)} · {End(display, durationMs, positionMs, speed)}"
        };
    }

    /// <summary>
    ///     The second line only helps when the chapter is shorter than the book; a book of one file without
    ///     chapters would show the same twice. Differences under a second are rounding.
    /// </summary>
    public static bool ShowSecondLine(double bookDurationMs, double chapterDurationMs) =>
        double.IsFinite(bookDurationMs) && double.IsFinite(chapterDurationMs) && bookDurationMs > 0 &&
        chapterDurationMs < bookDurationMs - 1000;

    /// <summary>Position in the whole book for a position in one of its files.</summary>
    public static double BookPositionMs(IEnumerable<double> sourceDurationsSeconds, int currentSourceIndex,
        double currentPositionMs) =>
        sourceDurationsSeconds.Take(Math.Max(0, currentSourceIndex)).Sum() * 1000 +
        (double.IsFinite(currentPositionMs) ? Math.Max(0, currentPositionMs) : 0);

    /// <summary>
    ///     The file and the position in it for a position in the whole book. A file boundary belongs
    ///     to the following file; positions past the end land at the end of the last file.
    /// </summary>
    public static (int SourceIndex, double PositionMs) Locate(IReadOnlyList<double> sourceDurationsSeconds,
        double bookPositionMs)
    {
        if (sourceDurationsSeconds.Count == 0 || !double.IsFinite(bookPositionMs) || bookPositionMs <= 0)
            return (0, 0);

        var remaining = bookPositionMs;
        for (var index = 0; index < sourceDurationsSeconds.Count; index++)
        {
            var fileMs = sourceDurationsSeconds[index] * 1000;
            if (remaining < fileMs) return (index, remaining);
            remaining -= fileMs;
        }

        return (sourceDurationsSeconds.Count - 1, sourceDurationsSeconds[^1] * 1000);
    }

    /// <summary>Reads a stored setting; anything that is not a known value gives the fallback.</summary>
    public static T Parse<T>(object? stored, T fallback) where T : struct, Enum =>
        int.TryParse(Convert.ToString(stored, CultureInfo.InvariantCulture), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var number) && Enum.IsDefined(typeof(T), number)
            ? (T)Enum.ToObject(typeof(T), number)
            : fallback;
}
