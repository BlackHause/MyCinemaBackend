using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KodiBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkUpdateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsManuallyVerified",
                table: "WebshareLinks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsManuallyVerified",
                table: "WebshareLinks");
        }
    }
}
