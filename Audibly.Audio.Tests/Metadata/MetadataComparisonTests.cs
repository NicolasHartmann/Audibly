using Audibly.Core.Metadata;
using Audibly.Models;
using Xunit;

namespace Audibly.Audio.Tests.Metadata;

public sealed class MetadataComparisonTests
{
    private static readonly BookMetadata Nothing = new(null, null, null, null, null, null, null);

    private static BookDetails Found(string? narrator = null, string? series = null, IReadOnlyList<string>? genres = null,
        DateTime? releaseDate = null, string? description = null) =>
        new("B1", description, narrator, series, null, genres ?? [], releaseDate);

    private static FieldChange Row(IReadOnlyList<FieldChange> rows, MetadataField field) =>
        rows.Single(row => row.Field == field);

    [Fact]
    public void Rows_come_in_fixed_order()
    {
        var rows = MetadataComparison.Compare(Nothing, Found());

        Assert.Equal(
            [MetadataField.Narrator, MetadataField.Series, MetadataField.SeriesPart, MetadataField.Genres,
             MetadataField.ReleaseDate, MetadataField.Description],
            rows.Select(row => row.Field));
    }

    [Fact]
    public void Empty_new_value_cannot_be_applied()
    {
        var row = Row(MetadataComparison.Compare(Nothing with { Series = "X" }, Found(series: null)), MetadataField.Series);

        Assert.False(row.CanApply);
        Assert.False(row.ApplyByDefault);
        Assert.Equal("X", row.Old);
    }

    [Fact]
    public void Different_value_is_checked_by_default()
    {
        var row = Row(MetadataComparison.Compare(Nothing, Found(narrator: "N")), MetadataField.Narrator);

        Assert.True(row.CanApply);
        Assert.True(row.ApplyByDefault);
        Assert.Equal("", row.Old);
        Assert.Equal("N", row.New);
    }

    [Fact]
    public void Equal_value_is_not_checked()
    {
        var row = Row(MetadataComparison.Compare(Nothing with { Narrator = "N" }, Found(narrator: "N")),
            MetadataField.Narrator);

        Assert.True(row.CanApply);
        Assert.False(row.ApplyByDefault);
    }

    [Fact]
    public void Release_date_compares_by_day()
    {
        var current = Nothing with { ReleaseDate = new DateTime(2010, 11, 4, 0, 0, 0) };

        var row = Row(MetadataComparison.Compare(current, Found(releaseDate: new DateTime(2010, 11, 4))),
            MetadataField.ReleaseDate);

        Assert.False(row.ApplyByDefault);
    }

    [Fact]
    public void Genres_are_shown_comma_separated()
    {
        var row = Row(MetadataComparison.Compare(Nothing with { Genres = "A\nB" }, Found(genres: ["A", "C"])),
            MetadataField.Genres);

        Assert.Equal("A, B", row.Old);
        Assert.Equal("A, C", row.New);
        Assert.True(row.ApplyByDefault);
    }

    [Fact]
    public void Apply_changes_only_chosen_fields()
    {
        var current = Nothing with { Series = "old series", Description = "old text" };

        var result = MetadataComparison.Apply(current, Found(narrator: "N", series: "S", description: "new text"),
            new HashSet<MetadataField> { MetadataField.Narrator });

        Assert.Equal("N", result.Narrator);
        Assert.Equal("old series", result.Series);
        Assert.Equal("old text", result.Description);
        Assert.Equal("B1", result.Asin);
    }

    [Fact]
    public void Apply_ignores_chosen_empty_field()
    {
        var result = MetadataComparison.Apply(Nothing with { Series = "X" }, Found(series: null),
            new HashSet<MetadataField> { MetadataField.Series });

        Assert.Equal("X", result.Series);
    }

    [Fact]
    public void Apply_stores_genres_one_per_line()
    {
        var result = MetadataComparison.Apply(Nothing, Found(genres: ["A", "B"]),
            new HashSet<MetadataField> { MetadataField.Genres });

        Assert.Equal("A\nB", result.Genres);
    }
}
