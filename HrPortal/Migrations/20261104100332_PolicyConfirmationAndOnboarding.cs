using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class PolicyConfirmationAndOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OnboardingCompletedAt",
                table: "Employees",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PolicyReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PolicyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolicyReadings_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PolicyReadings_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PolicyReadings_EmployeeId",
                table: "PolicyReadings",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyReadings_PolicyId_EmployeeId",
                table: "PolicyReadings",
                columns: new[] { "PolicyId", "EmployeeId" },
                unique: true);

            migrationBuilder.Sql(
                "UPDATE \"Employees\" SET \"OnboardingCompletedAt\" = now();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PolicyReadings");

            migrationBuilder.DropColumn(
                name: "OnboardingCompletedAt",
                table: "Employees");
        }
    }
}
