using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    public partial class RemoveDocumentExpiryAndOptionalUploader : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "EmployeeDocuments");

            migrationBuilder.AlterColumn<int>(
                name: "UploadedByEmployeeId",
                table: "EmployeeDocuments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "UploadedByEmployeeId",
                table: "EmployeeDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "EmployeeDocuments",
                type: "datetime2",
                nullable: true);
        }
    }
}
