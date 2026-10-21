using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddNextOfKinDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactAddress",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextOfKinConfirmedAt",
                table: "Employees",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmergencyContactAddress",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "NextOfKinConfirmedAt",
                table: "Employees");
        }
    }
}
