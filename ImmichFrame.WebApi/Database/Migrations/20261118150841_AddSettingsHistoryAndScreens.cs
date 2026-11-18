using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImmichFrame.WebApi.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsHistoryAndScreens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RestoredFromVersion",
                table: "SettingsDocuments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Screens",
                columns: table => new
                {
                    ClientIdentifier = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    FirstSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserAgent = table.Column<string>(type: "TEXT", nullable: true),
                    RemoteAddress = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Screens", x => x.ClientIdentifier);
                });

            migrationBuilder.CreateTable(
                name: "SettingsHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    RestoredFromVersion = table.Column<long>(type: "INTEGER", nullable: true),
                    Json = table.Column<string>(type: "TEXT", nullable: false),
                    SavedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettingsHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SettingsHistory_Version",
                table: "SettingsHistory",
                column: "Version");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Screens");

            migrationBuilder.DropTable(
                name: "SettingsHistory");

            migrationBuilder.DropColumn(
                name: "RestoredFromVersion",
                table: "SettingsDocuments");
        }
    }
}
