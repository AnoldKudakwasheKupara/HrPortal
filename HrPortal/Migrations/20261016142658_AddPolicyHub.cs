using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    public partial class AddPolicyHub : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Policies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequiresAcknowledgement = table.Column<bool>(type: "bit", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SupersedesPolicyId = table.Column<int>(type: "int", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Policies_Policies_SupersedesPolicyId",
                        column: x => x.SupersedesPolicyId,
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PolicyAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PolicyId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyAcknowledgements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolicyAcknowledgements_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PolicyAcknowledgements_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Policies_ReviewDate",
                table: "Policies",
                column: "ReviewDate");

            migrationBuilder.CreateIndex(
                name: "IX_Policies_Status",
                table: "Policies",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Policies_SupersedesPolicyId",
                table: "Policies",
                column: "SupersedesPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAcknowledgements_EmployeeId",
                table: "PolicyAcknowledgements",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAcknowledgements_PolicyId_EmployeeId",
                table: "PolicyAcknowledgements",
                columns: new[] { "PolicyId", "EmployeeId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PolicyAcknowledgements");

            migrationBuilder.DropTable(
                name: "Policies");
        }
    }
}
