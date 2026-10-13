using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    public partial class AddItActivityAndWeeklyReports : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoggedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ActionTaken = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RaisedByEmployeeId = table.Column<int>(type: "int", nullable: true),
                    RaisedByOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AssetId = table.Column<int>(type: "int", nullable: true),
                    ResolvedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsNotable = table.Column<bool>(type: "bit", nullable: false),
                    LoggedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItActivities_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItActivities_Employees_RaisedByEmployeeId",
                        column: x => x.RaisedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PreparedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExecutiveOverview = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinalisedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyReportPriorities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WeeklyReportId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CarriedForward = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyReportPriorities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyReportPriorities_WeeklyReports_WeeklyReportId",
                        column: x => x.WeeklyReportId,
                        principalTable: "WeeklyReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyReportSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WeeklyReportId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Heading = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Workstream = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WorkstreamStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyReportSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyReportSections_WeeklyReports_WeeklyReportId",
                        column: x => x.WeeklyReportId,
                        principalTable: "WeeklyReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItActivities_AssetId",
                table: "ItActivities",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ItActivities_LoggedOn",
                table: "ItActivities",
                column: "LoggedOn");

            migrationBuilder.CreateIndex(
                name: "IX_ItActivities_RaisedByEmployeeId",
                table: "ItActivities",
                column: "RaisedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ItActivities_Status_LoggedOn",
                table: "ItActivities",
                columns: new[] { "Status", "LoggedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReportPriorities_WeeklyReportId",
                table: "WeeklyReportPriorities",
                column: "WeeklyReportId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReports_PeriodStart",
                table: "WeeklyReports",
                column: "PeriodStart",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReportSections_WeeklyReportId",
                table: "WeeklyReportSections",
                column: "WeeklyReportId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItActivities");

            migrationBuilder.DropTable(
                name: "WeeklyReportPriorities");

            migrationBuilder.DropTable(
                name: "WeeklyReportSections");

            migrationBuilder.DropTable(
                name: "WeeklyReports");
        }
    }
}
