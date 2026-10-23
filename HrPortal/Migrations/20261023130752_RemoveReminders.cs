using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The five settings the reminder run was configured with.
            migrationBuilder.Sql(
                "DELETE FROM [AppSettings] " +
                "WHERE [Key] LIKE 'Reminders:%';");

            migrationBuilder.DropTable(
                name: "ReminderLogs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReminderLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    LastSentOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReferenceKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReminderLogs_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReminderLogs_EmployeeId",
                table: "ReminderLogs",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderLogs_Kind_ReferenceKey_EmployeeId",
                table: "ReminderLogs",
                columns: new[] { "Kind", "ReferenceKey", "EmployeeId" },
                unique: true);
        }
    }
}
