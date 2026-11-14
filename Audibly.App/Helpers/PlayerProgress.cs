using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Audibly.App.Helpers;

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
