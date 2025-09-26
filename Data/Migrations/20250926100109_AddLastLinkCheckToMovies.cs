using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KodiBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLastLinkCheckToMovies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastLinkCheck",
                table: "Movies",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLinkCheck",
                table: "Movies");
        }
    }
}
