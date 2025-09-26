using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KodiBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueTMDbIdToShow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TMDbId",
                table: "Shows",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TMDbId",
                table: "Movies",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebshareLinks_FileIdent",
                table: "WebshareLinks",
                column: "FileIdent",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shows_TMDbId",
                table: "Shows",
                column: "TMDbId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movies_TMDbId",
                table: "Movies",
                column: "TMDbId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebshareLinks_FileIdent",
                table: "WebshareLinks");

            migrationBuilder.DropIndex(
                name: "IX_Shows_TMDbId",
                table: "Shows");

            migrationBuilder.DropIndex(
                name: "IX_Movies_TMDbId",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "TMDbId",
                table: "Shows");

            migrationBuilder.DropColumn(
                name: "TMDbId",
                table: "Movies");
        }
    }
}
