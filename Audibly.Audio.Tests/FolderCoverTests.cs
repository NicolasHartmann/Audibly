using Audibly.Core.Import;
using Xunit;

namespace Audibly.Audio.Tests;

public sealed class FolderCoverTests
{
    [Fact]
    public void Well_known_name_wins_over_other_images()
    {
        Assert.Equal("Folder.JPG", FolderCover.Pick(["back.jpg", "01.mp3", "Folder.JPG", "scan.png"]));
    }

    [Fact]
    public void Cover_is_preferred_to_folder()
    {
        Assert.Equal("cover.png", FolderCover.Pick(["folder.jpg", "cover.png"]));
    }

    [Fact]
    public void Single_image_is_taken()
    {
        Assert.Equal("artwork.jpeg", FolderCover.Pick(["01.mp3", "artwork.jpeg", "notes.txt"]));
    }

    [Fact]
    public void Several_unnamed_images_give_no_cover()
    {
        Assert.Null(FolderCover.Pick(["a.jpg", "b.jpg"]));
        Assert.Null(FolderCover.Pick(["01.mp3"]));
    }

    [Fact]
    public void Missing_folder_gives_no_cover()
    {
        Assert.Null(FolderCover.Find(Path.Combine(Path.GetTempPath(), "audibly-missing-" + Guid.NewGuid())));
        Assert.Null(FolderCover.Find(null));
    }
}
