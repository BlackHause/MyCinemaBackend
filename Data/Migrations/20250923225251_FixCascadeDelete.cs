using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KodiBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WebshareLinks_Episodes_EpisodeId",
                table: "WebshareLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_WebshareLinks_Movies_MovieId",
                table: "WebshareLinks");

            migrationBuilder.AddForeignKey(
                name: "FK_WebshareLinks_Episodes_EpisodeId",
                table: "WebshareLinks",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WebshareLinks_Movies_MovieId",
                table: "WebshareLinks",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WebshareLinks_Episodes_EpisodeId",
                table: "WebshareLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_WebshareLinks_Movies_MovieId",
                table: "WebshareLinks");

            migrationBuilder.AddForeignKey(
                name: "FK_WebshareLinks_Episodes_EpisodeId",
                table: "WebshareLinks",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WebshareLinks_Movies_MovieId",
                table: "WebshareLinks",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id");
        }
    }
}
