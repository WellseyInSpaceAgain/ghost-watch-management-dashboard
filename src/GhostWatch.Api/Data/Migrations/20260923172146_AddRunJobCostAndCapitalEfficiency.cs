using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhostWatch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRunJobCostAndCapitalEfficiency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualJobCost",
                table: "EconomicRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CapitalTiedUp",
                table: "EconomicRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedJobCost",
                table: "EconomicRuns",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualJobCost",
                table: "EconomicRuns");

            migrationBuilder.DropColumn(
                name: "CapitalTiedUp",
                table: "EconomicRuns");

            migrationBuilder.DropColumn(
                name: "ExpectedJobCost",
                table: "EconomicRuns");
        }
    }
}
