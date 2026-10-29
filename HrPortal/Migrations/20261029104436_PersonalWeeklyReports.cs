using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class PersonalWeeklyReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WeeklyReports_PeriodStart",
                table: "WeeklyReports");

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "WeeklyReports",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReports_EmployeeId_PeriodStart",
                table: "WeeklyReports",
                columns: new[] { "EmployeeId", "PeriodStart" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WeeklyReports_Employees_EmployeeId",
                table: "WeeklyReports",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WeeklyReports_Employees_EmployeeId",
                table: "WeeklyReports");

            migrationBuilder.DropIndex(
                name: "IX_WeeklyReports_EmployeeId_PeriodStart",
                table: "WeeklyReports");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "WeeklyReports");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReports_PeriodStart",
                table: "WeeklyReports",
                column: "PeriodStart",
                unique: true);
        }
    }
}
