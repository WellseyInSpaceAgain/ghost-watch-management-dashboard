using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhostWatch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCapitalPools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultCapitalPoolId",
                table: "EconomyTracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CapitalPools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    AllocatedCapital = table.Column<decimal>(type: "TEXT", nullable: false),
                    TargetCapital = table.Column<decimal>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapitalPools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CapitalAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FromPoolId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ToPoolId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapitalAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CapitalAdjustments_CapitalPools_FromPoolId",
                        column: x => x.FromPoolId,
                        principalTable: "CapitalPools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CapitalAdjustments_CapitalPools_ToPoolId",
                        column: x => x.ToPoolId,
                        principalTable: "CapitalPools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EconomyTracks_DefaultCapitalPoolId",
                table: "EconomyTracks",
                column: "DefaultCapitalPoolId");

            migrationBuilder.CreateIndex(
                name: "IX_CapitalAdjustments_FromPoolId",
                table: "CapitalAdjustments",
                column: "FromPoolId");

            migrationBuilder.CreateIndex(
                name: "IX_CapitalAdjustments_ToPoolId",
                table: "CapitalAdjustments",
                column: "ToPoolId");

            migrationBuilder.CreateIndex(
                name: "IX_CapitalPools_Role",
                table: "CapitalPools",
                column: "Role",
                unique: true,
                filter: "\"Role\" <> 'Other' AND \"ArchivedAt\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_EconomyTracks_CapitalPools_DefaultCapitalPoolId",
                table: "EconomyTracks",
                column: "DefaultCapitalPoolId",
                principalTable: "CapitalPools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EconomyTracks_CapitalPools_DefaultCapitalPoolId",
                table: "EconomyTracks");

            migrationBuilder.DropTable(
                name: "CapitalAdjustments");

            migrationBuilder.DropTable(
                name: "CapitalPools");

            migrationBuilder.DropIndex(
                name: "IX_EconomyTracks_DefaultCapitalPoolId",
                table: "EconomyTracks");

            migrationBuilder.DropColumn(
                name: "DefaultCapitalPoolId",
                table: "EconomyTracks");
        }
    }
}
