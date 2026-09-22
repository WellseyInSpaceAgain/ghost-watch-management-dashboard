using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhostWatch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEconomicRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EconomicRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CapitalPoolId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RunType = table.Column<string>(type: "TEXT", nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ProductTypeId = table.Column<long>(type: "INTEGER", nullable: true),
                    ProductName = table.Column<string>(type: "TEXT", nullable: true),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpectedInputCost = table.Column<decimal>(type: "TEXT", nullable: true),
                    ExpectedOtherCost = table.Column<decimal>(type: "TEXT", nullable: true),
                    ExpectedRevenue = table.Column<decimal>(type: "TEXT", nullable: true),
                    ActualInputCost = table.Column<decimal>(type: "TEXT", nullable: true),
                    ActualOtherCost = table.Column<decimal>(type: "TEXT", nullable: true),
                    ActualRevenue = table.Column<decimal>(type: "TEXT", nullable: true),
                    ManufacturingHours = table.Column<decimal>(type: "TEXT", nullable: true),
                    ConcurrentSlots = table.Column<int>(type: "INTEGER", nullable: true),
                    TimeToSellDays = table.Column<decimal>(type: "TEXT", nullable: true),
                    Verdict = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomicRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EconomicRuns_CapitalPools_CapitalPoolId",
                        column: x => x.CapitalPoolId,
                        principalTable: "CapitalPools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EconomicRuns_EconomyTracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "EconomyTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RunJobs",
                columns: table => new
                {
                    CharacterId = table.Column<long>(type: "INTEGER", nullable: false),
                    JobId = table.Column<long>(type: "INTEGER", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunJobs", x => new { x.CharacterId, x.JobId });
                    table.ForeignKey(
                        name: "FK_RunJobs_EconomicRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "EconomicRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RunJobs_EveIndustryJobs_CharacterId_JobId",
                        columns: x => new { x.CharacterId, x.JobId },
                        principalTable: "EveIndustryJobs",
                        principalColumns: new[] { "CharacterId", "JobId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EconomicRuns_CapitalPoolId",
                table: "EconomicRuns",
                column: "CapitalPoolId");

            migrationBuilder.CreateIndex(
                name: "IX_EconomicRuns_TrackId",
                table: "EconomicRuns",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_RunJobs_RunId",
                table: "RunJobs",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunJobs");

            migrationBuilder.DropTable(
                name: "EconomicRuns");
        }
    }
}
