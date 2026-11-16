using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyRoles", x => new { x.UserId, x.CompanyId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_CompanyRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyRoles_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyRoles_CompanyId_RoleId",
                table: "CompanyRoles",
                columns: new[] { "CompanyId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyRoles_RoleId",
                table: "CompanyRoles",
                column: "RoleId");

            migrationBuilder.Sql(
                "INSERT INTO \"CompanyRoles\" (\"UserId\", \"CompanyId\", \"RoleId\") " +
                "SELECT ur.\"UserId\", 1, ur.\"RoleId\" " +
                "FROM \"AspNetUserRoles\" ur " +
                "JOIN \"AspNetRoles\" r ON r.\"Id\" = ur.\"RoleId\" " +
                "WHERE r.\"Name\" NOT IN ('Administrator', 'Employee');");

            migrationBuilder.Sql(
                "DELETE FROM \"AspNetUserRoles\" ur " +
                "USING \"AspNetRoles\" r " +
                "WHERE r.\"Id\" = ur.\"RoleId\" " +
                "AND r.\"Name\" NOT IN ('Administrator', 'Employee');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "INSERT INTO \"AspNetUserRoles\" (\"UserId\", \"RoleId\") " +
                "SELECT DISTINCT \"UserId\", \"RoleId\" FROM \"CompanyRoles\" " +
                "ON CONFLICT DO NOTHING;");

            migrationBuilder.DropTable(
                name: "CompanyRoles");
        }
    }
}
