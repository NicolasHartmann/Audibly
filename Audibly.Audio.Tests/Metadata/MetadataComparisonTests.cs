using Audibly.Core.Metadata;
using Audibly.Models;
using Xunit;

namespace Audibly.Audio.Tests.Metadata;

public sealed class MetadataComparisonTests
{
    private static readonly BookMetadata Nothing = new(null, null, null, null, null, null, null);

    private static BookDetails Found(string? narrator = null, string? series = null, IReadOnlyList<string>? genres = null,
        DateTime? releaseDate = null, string? description = null, string? title = null, string? image = null) =>
        new("B1", description, narrator, series, null, genres ?? [], releaseDate, title, image);

    private static FieldChange Row(IReadOnlyList<FieldChange> rows, MetadataField field) =>
        rows.Single(row => row.Field == field);

    [Fact]
    public void Rows_come_in_fixed_order()
    {
        var rows = MetadataComparison.Compare(Nothing, Found());

        Assert.Equal(
            [MetadataField.Title, MetadataField.Cover, MetadataField.Narrator, MetadataField.Series, MetadataField.SeriesPart, MetadataField.Tags,
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
    public void Tags_row_shows_union()
    {
        var row = Row(MetadataComparison.Compare(Nothing with { Tags = "Hörspiel" }, Found(genres: ["Fantasy"])),
            MetadataField.Tags);

        Assert.Equal("Hörspiel", row.Old);
        Assert.Equal("Hörspiel, Fantasy", row.New);
        Assert.True(row.ApplyByDefault);
    }

    [Fact]
    public void Tags_row_not_checked_when_nothing_new()
    {
        var row = Row(MetadataComparison.Compare(Nothing with { Tags = "Fantasy" }, Found(genres: ["fantasy"])),
            MetadataField.Tags);

        Assert.False(row.ApplyByDefault);
    }

    [Fact]
    public void Different_title_is_not_checked_by_default()
    {
        var row = Row(MetadataComparison.Compare(Nothing with { Title = "Kapitel 1" }, Found(title: "Gefährliche Liebe")),
            MetadataField.Title);

        Assert.True(row.CanApply);
        Assert.False(row.ApplyByDefault);
        Assert.Equal("Kapitel 1", row.Old);
        Assert.Equal("Gefährliche Liebe", row.New);
    }

    [Fact]
    public void Apply_renames_only_when_title_chosen()
    {
        var current = Nothing with { Title = "Kapitel 1" };
        var found = Found(title: "Gefährliche Liebe", narrator: "N");

        Assert.Equal("Kapitel 1", MetadataComparison.Apply(current, found, new HashSet<MetadataField> { MetadataField.Narrator }).Title);
        Assert.Equal("Gefährliche Liebe", MetadataComparison.Apply(current, found, new HashSet<MetadataField> { MetadataField.Title }).Title);
    }

    [Fact]
    public void Cover_is_checked_only_for_a_book_without_own_cover()
    {
        var found = Found(image: "https://m.media-amazon.com/images/I/x.jpg");

        var withoutCover = Row(MetadataComparison.Compare(Nothing, found, hasOwnCover: false), MetadataField.Cover);
        var withCover = Row(MetadataComparison.Compare(Nothing, found, hasOwnCover: true), MetadataField.Cover);

        Assert.True(withoutCover.CanApply);
        Assert.True(withoutCover.ApplyByDefault);
        Assert.Equal("https://m.media-amazon.com/images/I/x.jpg", withoutCover.New);
        Assert.True(withCover.CanApply);
        Assert.False(withCover.ApplyByDefault);
    }

    [Fact]
    public void Cover_without_image_cannot_be_applied()
    {
        var row = Row(MetadataComparison.Compare(Nothing, Found(), hasOwnCover: false), MetadataField.Cover);

        Assert.False(row.CanApply);
        Assert.False(row.ApplyByDefault);
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
    public void Apply_tags_keeps_own_tags()
    {
        var result = MetadataComparison.Apply(Nothing with { Tags = "Hörspiel" }, Found(genres: ["Fantasy"]),
            new HashSet<MetadataField> { MetadataField.Tags });

        Assert.Equal("Hörspiel\nFantasy", result.Tags);
        Assert.Equal("Fantasy", result.Genres);
    }

    [Fact]
    public void Apply_without_tags_keeps_everything()
    {
        var result = MetadataComparison.Apply(Nothing with { Tags = "Hörspiel", Genres = "Old" }, Found(genres: ["Fantasy"]),
            new HashSet<MetadataField>());

        Assert.Equal("Hörspiel", result.Tags);
        Assert.Equal("Old", result.Genres);
    }
}
