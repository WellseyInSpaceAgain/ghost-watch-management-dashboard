using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhostWatch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttentionAcknowledgements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttentionAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Rule = table.Column<string>(type: "TEXT", nullable: false),
                    SubjectType = table.Column<string>(type: "TEXT", nullable: true),
                    SubjectId = table.Column<string>(type: "TEXT", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttentionAcknowledgements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttentionAcknowledgements_Key",
                table: "AttentionAcknowledgements",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttentionAcknowledgements");
        }
    }
}
