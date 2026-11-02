using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class ReadSanctionsAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddedText",
                table: "SanctionsEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalIds",
                table: "SanctionsEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalScriptName",
                table: "SanctionsEntries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Page",
                table: "SanctionsEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Passports",
                table: "SanctionsEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemovedText",
                table: "SanctionsEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "SanctionsDocuments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "SanctionsDocuments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FullList");

            migrationBuilder.AddColumn<string>(
                name: "ListName",
                table: "SanctionsDocuments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PageCount",
                table: "SanctionsDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "SanctionsDocuments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsEntries_Reference",
                table: "SanctionsEntries",
                column: "Reference");

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsDocuments_Sha256",
                table: "SanctionsDocuments",
                column: "Sha256");

            migrationBuilder.Sql(
                "UPDATE \"SanctionsDocuments\" SET \"Kind\" = 'Notice' WHERE \"LooksLikeNotice\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SanctionsEntries_Reference",
                table: "SanctionsEntries");

            migrationBuilder.DropIndex(
                name: "IX_SanctionsDocuments_Sha256",
                table: "SanctionsDocuments");

            migrationBuilder.DropColumn(
                name: "AddedText",
                table: "SanctionsEntries");

            migrationBuilder.DropColumn(
                name: "NationalIds",
                table: "SanctionsEntries");

            migrationBuilder.DropColumn(
                name: "OriginalScriptName",
                table: "SanctionsEntries");

            migrationBuilder.DropColumn(
                name: "Page",
                table: "SanctionsEntries");

            migrationBuilder.DropColumn(
                name: "Passports",
                table: "SanctionsEntries");

            migrationBuilder.DropColumn(
                name: "RemovedText",
                table: "SanctionsEntries");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "SanctionsDocuments");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "SanctionsDocuments");

            migrationBuilder.DropColumn(
                name: "ListName",
                table: "SanctionsDocuments");

            migrationBuilder.DropColumn(
                name: "PageCount",
                table: "SanctionsDocuments");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "SanctionsDocuments");
        }
    }
}
