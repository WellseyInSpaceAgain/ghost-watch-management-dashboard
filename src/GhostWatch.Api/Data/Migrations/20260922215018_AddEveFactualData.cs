using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhostWatch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEveFactualData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EveIndustryJobs",
                columns: table => new
                {
                    CharacterId = table.Column<long>(type: "INTEGER", nullable: false),
                    JobId = table.Column<long>(type: "INTEGER", nullable: false),
                    ActivityId = table.Column<int>(type: "INTEGER", nullable: false),
                    BlueprintTypeId = table.Column<long>(type: "INTEGER", nullable: false),
                    ProductTypeId = table.Column<long>(type: "INTEGER", nullable: true),
                    Runs = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RawJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EveIndustryJobs", x => new { x.CharacterId, x.JobId });
                    table.ForeignKey(
                        name: "FK_EveIndustryJobs_EveCharacters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "EveCharacters",
                        principalColumn: "CharacterId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EveSections",
                columns: table => new
                {
                    CharacterId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Json = table.Column<string>(type: "TEXT", nullable: true),
                    AttemptedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EveSections", x => new { x.CharacterId, x.Name });
                    table.ForeignKey(
                        name: "FK_EveSections_EveCharacters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "EveCharacters",
                        principalColumn: "CharacterId",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EveIndustryJobs");

            migrationBuilder.DropTable(
                name: "EveSections");
        }
    }
}
