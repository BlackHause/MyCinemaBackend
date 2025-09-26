using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KodiBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinalBlacklistSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "WatchedAt",
                table: "HistoryEntries",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "MediaId",
                table: "HistoryEntries",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "HistoryEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Timestamp",
                table: "HistoryEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "HistoryEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEntries_Title_MediaType",
                table: "HistoryEntries",
                columns: new[] { "Title", "MediaType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HistoryEntries_Title_MediaType",
                table: "HistoryEntries");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "HistoryEntries");

            migrationBuilder.DropColumn(
                name: "Timestamp",
                table: "HistoryEntries");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "HistoryEntries");

            migrationBuilder.AlterColumn<DateTime>(
                name: "WatchedAt",
                table: "HistoryEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MediaId",
                table: "HistoryEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
