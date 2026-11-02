using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class ImportSanctionsClientsWithColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "SanctionsClients",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCompany",
                table: "SanctionsClients",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nationality",
                table: "SanctionsClients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassportNumber",
                table: "SanctionsClients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNumber",
                table: "SanctionsClients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RowNumber",
                table: "SanctionsClients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ColumnMap",
                table: "SanctionsClientLists",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "SanctionsClientLists",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Problems",
                table: "SanctionsClientLists",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RowsDuplicated",
                table: "SanctionsClientLists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RowsImported",
                table: "SanctionsClientLists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RowsInvalid",
                table: "SanctionsClientLists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RowsReceived",
                table: "SanctionsClientLists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "SanctionsClientLists",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Imported");

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsClientLists_Status_UploadedAt",
                table: "SanctionsClientLists",
                columns: new[] { "Status", "UploadedAt" });

            migrationBuilder.Sql(
                "UPDATE \"SanctionsClientLists\" l SET \"RowsImported\" = c.n, \"RowsReceived\" = c.n " +
                "FROM (SELECT \"SanctionsClientListId\" AS id, COUNT(*) AS n FROM \"SanctionsClients\" " +
                "GROUP BY \"SanctionsClientListId\") c WHERE c.id = l.\"Id\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SanctionsClientLists_Status_UploadedAt",
                table: "SanctionsClientLists");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "SanctionsClients");

            migrationBuilder.DropColumn(
                name: "IsCompany",
                table: "SanctionsClients");

            migrationBuilder.DropColumn(
                name: "Nationality",
                table: "SanctionsClients");

            migrationBuilder.DropColumn(
                name: "PassportNumber",
                table: "SanctionsClients");

            migrationBuilder.DropColumn(
                name: "RegistrationNumber",
                table: "SanctionsClients");

            migrationBuilder.DropColumn(
                name: "RowNumber",
                table: "SanctionsClients");

            migrationBuilder.DropColumn(
                name: "ColumnMap",
                table: "SanctionsClientLists");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "SanctionsClientLists");

            migrationBuilder.DropColumn(
                name: "Problems",
                table: "SanctionsClientLists");

            migrationBuilder.DropColumn(
                name: "RowsDuplicated",
                table: "SanctionsClientLists");

            migrationBuilder.DropColumn(
                name: "RowsImported",
                table: "SanctionsClientLists");

            migrationBuilder.DropColumn(
                name: "RowsInvalid",
                table: "SanctionsClientLists");

            migrationBuilder.DropColumn(
                name: "RowsReceived",
                table: "SanctionsClientLists");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SanctionsClientLists");
        }
    }
}
