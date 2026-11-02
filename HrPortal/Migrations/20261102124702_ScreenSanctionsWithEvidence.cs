using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class ScreenSanctionsWithEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Evidence",
                table: "SanctionsConcerns",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Strength",
                table: "SanctionsConcerns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectFingerprint",
                table: "SanctionsConcerns",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Evidence",
                table: "SanctionsConcerns");

            migrationBuilder.DropColumn(
                name: "Strength",
                table: "SanctionsConcerns");

            migrationBuilder.DropColumn(
                name: "SubjectFingerprint",
                table: "SanctionsConcerns");
        }
    }
}
