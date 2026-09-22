using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhostWatch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CharacterTracks",
                columns: table => new
                {
                    CharacterId = table.Column<long>(type: "INTEGER", nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterTracks", x => new { x.CharacterId, x.TrackId });
                    table.ForeignKey(
                        name: "FK_CharacterTracks_EconomyTracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "EconomyTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CharacterTracks_EveCharacters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "EveCharacters",
                        principalColumn: "CharacterId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ManagedAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Subscription = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CharacterPlans",
                columns: table => new
                {
                    CharacterId = table.Column<long>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Assignment = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterPlans", x => x.CharacterId);
                    table.ForeignKey(
                        name: "FK_CharacterPlans_EveCharacters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "EveCharacters",
                        principalColumn: "CharacterId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CharacterPlans_ManagedAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "ManagedAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterPlans_AccountId",
                table: "CharacterPlans",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterTracks_TrackId",
                table: "CharacterTracks",
                column: "TrackId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterPlans");

            migrationBuilder.DropTable(
                name: "CharacterTracks");

            migrationBuilder.DropTable(
                name: "ManagedAccounts");
        }
    }
}
