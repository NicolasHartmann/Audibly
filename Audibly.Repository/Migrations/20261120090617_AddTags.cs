using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audibly.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Audiobooks",
                type: "TEXT",
                nullable: true);

            // the genres found online so far become the books' first tags
            migrationBuilder.Sql("UPDATE Audiobooks SET Tags = Genres WHERE Genres IS NOT NULL AND Genres <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Audiobooks");
        }
    }
}
