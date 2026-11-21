using Audibly.Core;
using Xunit;

namespace Audibly.Audio.Tests;

public sealed class PositionHistoryTests : IDisposable
{
    private static readonly DateTime When = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
    private readonly string _file = Path.Combine(Path.GetTempPath(), "audibly-history-" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose() => File.Delete(_file);

    private static StopPoint At(double minutes) => new(minutes * 60_000, "Chapter", When);

    [Fact]
    public void Newest_first_and_at_most_five()
    {
        var points = new List<StopPoint>();
        for (var minute = 1; minute <= 7; minute++) points = PositionHistory.Add(points, At(minute * 10));
        Assert.Equal([70.0, 60, 50, 40, 30], points.Select(point => point.PositionMs / 60_000));
    }

    [Fact]
    public void Same_place_is_kept_once()
    {
        var points = PositionHistory.Add([At(10), At(20)], At(10.2));
        Assert.Equal([10.2, 20], points.Select(point => point.PositionMs / 60_000));
    }

    [Fact]
    public void Points_survive_a_restart_per_book()
    {
        var book = Guid.NewGuid();
        var other = Guid.NewGuid();
        new PositionHistory(_file).Add(book, At(5));
        new PositionHistory(_file).Add(other, At(9));

        var reloaded = new PositionHistory(_file);
        Assert.Equal(At(5), Assert.Single(reloaded.For(book)));
        Assert.Equal(At(9), Assert.Single(reloaded.For(other)));
        reloaded.Remove(book);
        Assert.Empty(new PositionHistory(_file).For(book));
    }

    [Fact]
    public void Broken_file_gives_empty_history()
    {
        File.WriteAllText(_file, "not json");
        Assert.Empty(new PositionHistory(_file).For(Guid.NewGuid()));
    }
}
