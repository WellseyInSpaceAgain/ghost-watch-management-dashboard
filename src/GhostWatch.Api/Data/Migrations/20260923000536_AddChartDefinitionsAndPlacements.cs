using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhostWatch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChartDefinitionsAndPlacements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChartDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChartPlacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChartDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PageType = table.Column<string>(type: "TEXT", nullable: false),
                    PageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Width = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartPlacements", x => x.Id);
                    table.CheckConstraint("CK_ChartPlacement_Page", "(PageType = 'Dashboard' AND PageId IS NULL) OR (PageType = 'Track' AND PageId IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ChartPlacements_ChartDefinitions_ChartDefinitionId",
                        column: x => x.ChartDefinitionId,
                        principalTable: "ChartDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChartPlacements_EconomyTracks_PageId",
                        column: x => x.PageId,
                        principalTable: "EconomyTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartPlacements_ChartDefinitionId",
                table: "ChartPlacements",
                column: "ChartDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChartPlacements_PageId",
                table: "ChartPlacements",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChartPlacements_PageType_PageId_SortOrder",
                table: "ChartPlacements",
                columns: new[] { "PageType", "PageId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartPlacements");

            migrationBuilder.DropTable(
                name: "ChartDefinitions");
        }
    }
}
