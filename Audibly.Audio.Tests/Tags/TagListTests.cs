using Audibly.Core.Tags;
using Xunit;

namespace Audibly.Audio.Tests.Tags;

public sealed class TagListTests
{
    [Fact]
    public void Parse_drops_blank_lines_and_spaces()
    {
        Assert.Equal(["Fantasy", "Krimi"], TagList.Parse(" Fantasy \n\n Krimi\r\n"));
        Assert.Empty(TagList.Parse(null));
    }

    [Fact]
    public void Parse_removes_case_duplicates() =>
        Assert.Equal(["Fantasy"], TagList.Parse("Fantasy\nfantasy"));

    [Fact]
    public void Format_empty_is_null()
    {
        Assert.Null(TagList.Format([]));
        Assert.Equal("A\nB", TagList.Format(["A", "B"]));
    }

    [Fact]
    public void Add_uses_known_spelling() =>
        Assert.Equal(["Fantasy"], TagList.Add([], ["fantasy"], ["Fantasy"]));

    [Fact]
    public void Add_keeps_existing_and_skips_duplicates() =>
        Assert.Equal(["Krimi", "Hörspiel"], TagList.Add(["Krimi"], ["krimi", "Hörspiel"], []));

    [Fact]
    public void Remove_is_case_insensitive()
    {
        Assert.Equal(["Krimi"], TagList.Remove(["Fantasy", "Krimi"], ["fantasy"]));
        Assert.Equal(["Krimi"], TagList.Remove(["Krimi"], ["Fantasy"]));
    }

    [Fact]
    public void AllTags_sorted_and_distinct() =>
        Assert.Equal(["A", "B"], TagList.AllTags(["B\nA", null, "a"]));

    [Fact]
    public void Matches_any_ticked_tag()
    {
        Assert.True(TagList.Matches(["Fantasy"], []));
        Assert.True(TagList.Matches(["Fantasy"], ["Krimi", "fantasy"]));
        Assert.False(TagList.Matches([], ["Krimi"]));
    }
}
