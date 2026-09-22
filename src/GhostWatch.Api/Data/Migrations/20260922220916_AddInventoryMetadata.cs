using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhostWatch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Warning",
                table: "EveSections",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PublicEveLookups",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Json = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicEveLookups", x => x.Key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicEveLookups");

            migrationBuilder.DropColumn(
                name: "Warning",
                table: "EveSections");
        }
    }
}
