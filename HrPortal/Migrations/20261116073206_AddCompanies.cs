using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HomeCompanyId",
                table: "Employees",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeCompanies",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCompanies", x => new { x.EmployeeId, x.CompanyId });
                    table.ForeignKey(
                        name: "FK_EmployeeCompanies_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeCompanies_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Companies",
                columns: new[] { "Id", "Key", "Name" },
                values: new object[] { 1, "intellego", "Intellego Investment Consultants" });

            migrationBuilder.InsertData(
                table: "Companies",
                columns: new[] { "Id", "Key", "Name" },
                values: new object[] { 2, "mmc", "MMC Capital" });

            migrationBuilder.Sql(
                "INSERT INTO \"EmployeeCompanies\" (\"EmployeeId\", \"CompanyId\") " +
                "SELECT \"Id\", 1 FROM \"Employees\";");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_HomeCompanyId",
                table: "Employees",
                column: "HomeCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Key",
                table: "Companies",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompanies_CompanyId",
                table: "EmployeeCompanies",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Companies_HomeCompanyId",
                table: "Employees",
                column: "HomeCompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Companies_HomeCompanyId",
                table: "Employees");

            migrationBuilder.DropTable(
                name: "EmployeeCompanies");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Employees_HomeCompanyId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "HomeCompanyId",
                table: "Employees");
        }
    }
}
