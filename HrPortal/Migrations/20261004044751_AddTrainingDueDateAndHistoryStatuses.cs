using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    public partial class AddTrainingDueDateAndHistoryStatuses : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "TrainingAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewStatus",
                table: "LeaveRequestHistories",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<string>(
                name: "PreviousStatus",
                table: "LeaveRequestHistories",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Draft");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "TrainingAssignments");

            migrationBuilder.DropColumn(
                name: "NewStatus",
                table: "LeaveRequestHistories");

            migrationBuilder.DropColumn(
                name: "PreviousStatus",
                table: "LeaveRequestHistories");
        }
    }
}
