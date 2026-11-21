using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Audibly.Core;

/// <summary>A place in a book the listener left: paused there, or jumped away from it.</summary>
public sealed record StopPoint(double PositionMs, string Chapter, DateTime SavedUtc);

/// <summary>
///     The last few places per book, newest first, so an accidental skip can be undone. Kept in a
///     small JSON file next to the library; it is not part of the sync.
/// </summary>
public sealed class PositionHistory(string filePath)
{
    public const int MaxPoints = 5;

    /// <summary>Points closer than this count as the same place; the newer one replaces the older.</summary>
    public const double SamePlaceMs = 30_000;

    /// <summary>A position change larger than this is a jump, not playback.</summary>
    public const double JumpMs = 60_000;

    private Dictionary<Guid, List<StopPoint>>? _points;

    public IReadOnlyList<StopPoint> For(Guid bookId) =>
        Load().TryGetValue(bookId, out var points) ? points : [];

    public void Add(Guid bookId, StopPoint point)
    {
        var all = Load();
        all[bookId] = Add(all.TryGetValue(bookId, out var points) ? points : [], point);
        Save(all);
    }

    public void Remove(Guid bookId)
    {
        if (Load().Remove(bookId)) Save(Load());
    }

    /// <summary>Puts a point first, drops points at the same place and keeps at most <see cref="MaxPoints" />.</summary>
    public static List<StopPoint> Add(IEnumerable<StopPoint> points, StopPoint point) =>
        new[] { point }
            .Concat(points.Where(old => Math.Abs(old.PositionMs - point.PositionMs) >= SamePlaceMs))
            .Take(MaxPoints)
            .ToList();

    private Dictionary<Guid, List<StopPoint>> Load()
    {
        if (_points != null) return _points;
        try
        {
            _points = File.Exists(filePath)
                ? JsonSerializer.Deserialize<Dictionary<Guid, List<StopPoint>>>(File.ReadAllText(filePath)) ?? []
                : [];
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            _points = [];
        }

        return _points;
    }

    private void Save(Dictionary<Guid, List<StopPoint>> all)
    {
        try
        {
            var temp = filePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(all));
            File.Move(temp, filePath, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // the history is a convenience; losing a point must never disturb playback
        }
    }
}
