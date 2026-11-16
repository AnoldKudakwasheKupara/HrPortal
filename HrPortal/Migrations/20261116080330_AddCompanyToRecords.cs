using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyToRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RepositoryFolders_Name",
                table: "RepositoryFolders");

            migrationBuilder.DropIndex(
                name: "IX_Departments_Name",
                table: "Departments");

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "WeeklyReports",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "TrainingCourses",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "RepositoryFolders",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Policies",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Departments",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"Departments\" SET \"CompanyId\" = 1 " +
                "WHERE upper(trim(\"Name\")) NOT IN (" +
                "'IT', 'ICT', 'I.T.', 'I.T', 'IT DEPARTMENT', 'ICT DEPARTMENT', " +
                "'INFORMATION TECHNOLOGY', 'INFORMATION TECHNOLOGY DEPARTMENT', " +
                "'INFORMATION AND COMMUNICATION TECHNOLOGY', " +
                "'INFORMATION & COMMUNICATION TECHNOLOGY', " +
                "'INFORMATION COMMUNICATION TECHNOLOGY', " +
                "'IT SUPPORT', 'IT SERVICES', 'TECHNOLOGY');");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReports_CompanyId",
                table: "WeeklyReports",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingCourses_CompanyId",
                table: "TrainingCourses",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryFolders_CompanyId",
                table: "RepositoryFolders",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryFolders_CompanyId_Name",
                table: "RepositoryFolders",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Policies_CompanyId",
                table: "Policies",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_CompanyId_Name",
                table: "Departments",
                columns: new[] { "CompanyId", "Name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Companies_CompanyId",
                table: "Departments",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Policies_Companies_CompanyId",
                table: "Policies",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RepositoryFolders_Companies_CompanyId",
                table: "RepositoryFolders",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrainingCourses_Companies_CompanyId",
                table: "TrainingCourses",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeeklyReports_Companies_CompanyId",
                table: "WeeklyReports",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Companies_CompanyId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_Policies_Companies_CompanyId",
                table: "Policies");

            migrationBuilder.DropForeignKey(
                name: "FK_RepositoryFolders_Companies_CompanyId",
                table: "RepositoryFolders");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainingCourses_Companies_CompanyId",
                table: "TrainingCourses");

            migrationBuilder.DropForeignKey(
                name: "FK_WeeklyReports_Companies_CompanyId",
                table: "WeeklyReports");

            migrationBuilder.DropIndex(
                name: "IX_WeeklyReports_CompanyId",
                table: "WeeklyReports");

            migrationBuilder.DropIndex(
                name: "IX_TrainingCourses_CompanyId",
                table: "TrainingCourses");

            migrationBuilder.DropIndex(
                name: "IX_RepositoryFolders_CompanyId",
                table: "RepositoryFolders");

            migrationBuilder.DropIndex(
                name: "IX_RepositoryFolders_CompanyId_Name",
                table: "RepositoryFolders");

            migrationBuilder.DropIndex(
                name: "IX_Policies_CompanyId",
                table: "Policies");

            migrationBuilder.DropIndex(
                name: "IX_Departments_CompanyId_Name",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "WeeklyReports");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "TrainingCourses");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "RepositoryFolders");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Departments");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryFolders_Name",
                table: "RepositoryFolders",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name",
                unique: true);
        }
    }
}
