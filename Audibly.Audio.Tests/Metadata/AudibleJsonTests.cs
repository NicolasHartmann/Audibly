using Audibly.Core.Metadata;
using Xunit;

namespace Audibly.Audio.Tests.Metadata;

public sealed class AudibleJsonTests
{
    [Fact]
    public void ParseSearch_reads_hit()
    {
        var hits = AudibleJson.ParseSearch(Fixtures.Read("search-de.json"), "de");

        var hit = Assert.Single(hits, h => h.Asin == "B008CND4RS");
        Assert.Equal("Gefährliche Liebe", hit.Title);
        Assert.Equal("Suzanne Collins", hit.Author);
        Assert.Equal("Maria Koschny", hit.Narrators);
        Assert.Equal("Die Tribute von Panem", hit.Series);
        Assert.Equal("2", hit.SeriesPart);
        Assert.Equal(576, hit.RuntimeMinutes);
        Assert.Equal(new DateTime(2012, 6, 19), hit.ReleaseDate);
        Assert.Equal("german", hit.Language);
        Assert.Equal("de", hit.Region);
        Assert.EndsWith(".jpg", hit.ImageUrl);
    }

    [Fact]
    public void ParseSearch_skips_products_without_runtime()
    {
        var hits = AudibleJson.ParseSearch("{\"products\":[{\"asin\":\"3838795679\",\"title\":\"X\"}]}", "de");

        Assert.Empty(hits);
    }

    [Fact]
    public void ParseDetails_reads_series_and_genres()
    {
        var details = AudibleJson.ParseDetails(Fixtures.Read("audnexus-panem2.json"));

        Assert.Equal("B008CND4RS", details.Asin);
        Assert.Equal("Die Tribute von Panem", details.Series);
        Assert.Equal("2", details.SeriesPart);
        Assert.Equal("Maria Koschny", details.Narrator);
        Assert.Equal(new DateTime(2012, 6, 19), details.ReleaseDate!.Value.Date);
        Assert.False(string.IsNullOrWhiteSpace(details.Description));
    }

    [Fact]
    public void ParseDetails_lists_genres_before_tags()
    {
        var details = AudibleJson.ParseDetails(Fixtures.Read("audnexus-lotr.json"));

        Assert.Equal(["Literatur & Belletristik", "Science Fiction & Fantasy"], details.Genres.Take(2));
        Assert.Contains("Epic Fantasy", details.Genres);
        Assert.Equal("Manfred Steffen, Rufus Beck, Hans Peter Hallwachs", details.Narrator);
        Assert.Null(details.Series);
        Assert.Null(details.SeriesPart);
    }

    [Fact]
    public void ParseDetails_without_optional_fields()
    {
        var details = AudibleJson.ParseDetails("{\"asin\":\"X\"}");

        Assert.Equal("X", details.Asin);
        Assert.Null(details.Description);
        Assert.Null(details.Narrator);
        Assert.Null(details.Series);
        Assert.Null(details.SeriesPart);
        Assert.Null(details.ReleaseDate);
        Assert.Empty(details.Genres);
    }

    [Fact]
    public void ParseDetails_keeps_release_day()
    {
        var details = AudibleJson.ParseDetails("{\"asin\":\"X\",\"releaseDate\":\"2010-11-04T00:00:00.000Z\"}");

        var date = details.ReleaseDate!.Value;
        Assert.Equal((2010, 11, 4), (date.Year, date.Month, date.Day));
        Assert.Equal(DateTimeKind.Unspecified, date.Kind);
    }

    [Fact]
    public void HtmlToText_paragraphs_and_entities()
    {
        var text = AudibleJson.HtmlToText("<p>A &amp; B</p><p>C<br>D</p><b>E</b>");

        Assert.Equal("A & B\n\nC\nD\n\nE", text);
    }

    [Fact]
    public void Merge_de_first_and_dedupes()
    {
        AudibleHit Hit(string asin, string region) =>
            new(asin, region, asin, "", "", null, null, 60, null, null, null);
        var de = new[] { Hit("a", "de"), Hit("b", "de") };
        var us = new[] { Hit("b", "us"), Hit("c", "us") };

        var merged = AudibleJson.Merge(de, us);

        Assert.Equal(["a", "b", "c"], merged.Select(h => h.Asin));
        Assert.Equal("de", merged[1].Region);
    }
}
