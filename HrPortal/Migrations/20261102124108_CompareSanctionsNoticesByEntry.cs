using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class CompareSanctionsNoticesByEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ComparisonNote",
                table: "SanctionsReviews",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Inconclusive",
                table: "SanctionsReviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "NotComparedCount",
                table: "SanctionsReviews",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UnchangedCount",
                table: "SanctionsReviews",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EntryCount",
                table: "SanctionsDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "SanctionsChanges",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Page",
                table: "SanctionsChanges",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"SanctionsDocuments\" d SET \"EntryCount\" = " +
                "(SELECT COUNT(*) FROM \"SanctionsEntries\" e WHERE e.\"SanctionsDocumentId\" = d.\"Id\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComparisonNote",
                table: "SanctionsReviews");

            migrationBuilder.DropColumn(
                name: "Inconclusive",
                table: "SanctionsReviews");

            migrationBuilder.DropColumn(
                name: "NotComparedCount",
                table: "SanctionsReviews");

            migrationBuilder.DropColumn(
                name: "UnchangedCount",
                table: "SanctionsReviews");

            migrationBuilder.DropColumn(
                name: "EntryCount",
                table: "SanctionsDocuments");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "SanctionsChanges");

            migrationBuilder.DropColumn(
                name: "Page",
                table: "SanctionsChanges");
        }
    }
}
