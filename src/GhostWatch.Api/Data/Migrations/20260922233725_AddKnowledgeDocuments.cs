using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhostWatch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PlaybookId",
                table: "EconomicRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EconomicRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    RecordType = table.Column<string>(type: "TEXT", nullable: false),
                    MarkdownBody = table.Column<string>(type: "TEXT", nullable: false),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomicRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Playbooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    MarkdownBody = table.Column<string>(type: "TEXT", nullable: false),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playbooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlaybookId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RecordId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CharacterId = table.Column<long>(type: "INTEGER", nullable: true),
                    RelatedPlaybookId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ObjectiveId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CapitalPoolId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeLinks", x => x.Id);
                    table.CheckConstraint("CK_KnowledgeLink_Owner", "(PlaybookId IS NOT NULL) + (RecordId IS NOT NULL) = 1");
                    table.CheckConstraint("CK_KnowledgeLink_Target", "(TrackId IS NOT NULL) + (RunId IS NOT NULL) + (CharacterId IS NOT NULL) + (RelatedPlaybookId IS NOT NULL) + (ObjectiveId IS NOT NULL) + (CapitalPoolId IS NOT NULL) = 1");
                    table.ForeignKey(
                        name: "FK_KnowledgeLinks_CapitalPools_CapitalPoolId",
                        column: x => x.CapitalPoolId,
                        principalTable: "CapitalPools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeLinks_EconomicRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "EconomicRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeLinks_EconomicRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "EconomicRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeLinks_EconomyTracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "EconomyTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeLinks_EveCharacters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "EveCharacters",
                        principalColumn: "CharacterId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeLinks_Objectives_ObjectiveId",
                        column: x => x.ObjectiveId,
                        principalTable: "Objectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeLinks_Playbooks_PlaybookId",
                        column: x => x.PlaybookId,
                        principalTable: "Playbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeLinks_Playbooks_RelatedPlaybookId",
                        column: x => x.RelatedPlaybookId,
                        principalTable: "Playbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlaybookRevisions",
                columns: table => new
                {
                    PlaybookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    MarkdownBody = table.Column<string>(type: "TEXT", nullable: false),
                    SavedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybookRevisions", x => new { x.PlaybookId, x.Version });
                    table.ForeignKey(
                        name: "FK_PlaybookRevisions_Playbooks_PlaybookId",
                        column: x => x.PlaybookId,
                        principalTable: "Playbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EconomicRuns_PlaybookId",
                table: "EconomicRuns",
                column: "PlaybookId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeLinks_CapitalPoolId",
                table: "KnowledgeLinks",
                column: "CapitalPoolId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeLinks_CharacterId",
                table: "KnowledgeLinks",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeLinks_ObjectiveId",
                table: "KnowledgeLinks",
                column: "ObjectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeLinks_PlaybookId",
                table: "KnowledgeLinks",
                column: "PlaybookId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeLinks_RecordId",
                table: "KnowledgeLinks",
                column: "RecordId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeLinks_RelatedPlaybookId",
                table: "KnowledgeLinks",
                column: "RelatedPlaybookId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeLinks_RunId",
                table: "KnowledgeLinks",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeLinks_TrackId",
                table: "KnowledgeLinks",
                column: "TrackId");

            migrationBuilder.AddForeignKey(
                name: "FK_EconomicRuns_Playbooks_PlaybookId",
                table: "EconomicRuns",
                column: "PlaybookId",
                principalTable: "Playbooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EconomicRuns_Playbooks_PlaybookId",
                table: "EconomicRuns");

            migrationBuilder.DropTable(
                name: "KnowledgeLinks");

            migrationBuilder.DropTable(
                name: "PlaybookRevisions");

            migrationBuilder.DropTable(
                name: "EconomicRecords");

            migrationBuilder.DropTable(
                name: "Playbooks");

            migrationBuilder.DropIndex(
                name: "IX_EconomicRuns_PlaybookId",
                table: "EconomicRuns");

            migrationBuilder.DropColumn(
                name: "PlaybookId",
                table: "EconomicRuns");
        }
    }
}
