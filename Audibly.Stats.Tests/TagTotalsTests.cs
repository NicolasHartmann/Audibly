using Audibly.Stats;

namespace Audibly.Stats.Tests;

public sealed class TagTotalsTests
{
    private static readonly Guid Book1 = Guid.NewGuid();
    private static readonly Guid Book2 = Guid.NewGuid();

    private static StatsData Data(params ListeningSlice[] slices) => new(slices, []);

    private static ListeningSlice Slice(Guid book, double seconds, DateOnly? day = null) =>
        new(day ?? new DateOnly(2026, 11, 1), 10, book, seconds, 0);

    [Fact]
    public void ByTag_counts_book_in_each_tag()
    {
        var totals = StatsSummary.ByTag(Data(Slice(Book1, 60)),
            new Dictionary<Guid, IReadOnlyList<string>> { [Book1] = ["A", "B"] });

        Assert.Equal(60, totals.Single(t => t.Tag == "A").ListenedSeconds);
        Assert.Equal(60, totals.Single(t => t.Tag == "B").ListenedSeconds);
    }

    [Fact]
    public void ByTag_unknown_or_untagged_book_is_no_tag()
    {
        var totals = StatsSummary.ByTag(Data(Slice(Book1, 30), Slice(Book2, 5)),
            new Dictionary<Guid, IReadOnlyList<string>> { [Book2] = [] });

        Assert.Equal(35, totals.Single(t => t.Tag == StatsSummary.NoTag).ListenedSeconds);
    }

    [Fact]
    public void ByTag_respects_from()
    {
        var totals = StatsSummary.ByTag(
            Data(Slice(Book1, 10, new DateOnly(2026, 1, 1)), Slice(Book1, 20, new DateOnly(2026, 2, 1))),
            new Dictionary<Guid, IReadOnlyList<string>> { [Book1] = ["A"] }, new DateOnly(2026, 2, 1));

        Assert.Equal(20, totals.Single().ListenedSeconds);
    }

    [Fact]
    public void ByTag_sorted_descending()
    {
        var totals = StatsSummary.ByTag(Data(Slice(Book1, 10), Slice(Book2, 50)),
            new Dictionary<Guid, IReadOnlyList<string>> { [Book1] = ["Small"], [Book2] = ["Big"] });

        Assert.Equal(["Big", "Small"], totals.Select(t => t.Tag));
    }
}
