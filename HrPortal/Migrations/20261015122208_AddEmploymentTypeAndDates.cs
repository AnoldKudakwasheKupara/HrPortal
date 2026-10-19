using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    public partial class AddEmploymentTypeAndDates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ContractEndDate",
                table: "Employees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmploymentType",
                table: "Employees",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Permanent");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Employees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeavingReason",
                table: "Employees",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ContractEndDate",
                table: "Employees",
                column: "ContractEndDate");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_ContractEndDate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ContractEndDate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "EmploymentType",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "LeavingReason",
                table: "Employees");
        }
    }
}
