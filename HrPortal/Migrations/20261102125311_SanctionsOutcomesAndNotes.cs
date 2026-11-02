using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class SanctionsOutcomesAndNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "SanctionsConcerns",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<DateTime>(
                name: "EscalatedAt",
                table: "SanctionsConcerns",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalatedTo",
                table: "SanctionsConcerns",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SanctionsConcernNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SanctionsConcernId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanctionsConcernNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SanctionsConcernNotes_SanctionsConcerns_SanctionsConcernId",
                        column: x => x.SanctionsConcernId,
                        principalTable: "SanctionsConcerns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsConcerns_SubjectType_SubjectId_Reference",
                table: "SanctionsConcerns",
                columns: new[] { "SubjectType", "SubjectId", "Reference" });

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsConcernNotes_SanctionsConcernId",
                table: "SanctionsConcernNotes",
                column: "SanctionsConcernId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SanctionsConcernNotes");

            migrationBuilder.DropIndex(
                name: "IX_SanctionsConcerns_SubjectType_SubjectId_Reference",
                table: "SanctionsConcerns");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                table: "SanctionsConcerns");

            migrationBuilder.DropColumn(
                name: "EscalatedTo",
                table: "SanctionsConcerns");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "SanctionsConcerns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);
        }
    }
}
