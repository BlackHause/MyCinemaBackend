using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KodiBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMultipleLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileIdent",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "FileIdent",
                table: "Episodes");

            migrationBuilder.CreateTable(
                name: "WebshareLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileIdent = table.Column<string>(type: "TEXT", nullable: true),
                    Quality = table.Column<string>(type: "TEXT", nullable: true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebshareLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebshareLinks_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "Episodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WebshareLinks_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebshareLinks_EpisodeId",
                table: "WebshareLinks",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_WebshareLinks_MovieId",
                table: "WebshareLinks",
                column: "MovieId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebshareLinks");

            migrationBuilder.AddColumn<string>(
                name: "FileIdent",
                table: "Movies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileIdent",
                table: "Episodes",
                type: "TEXT",
                nullable: true);
        }
    }
}
