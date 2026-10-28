using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingCourseTrainerEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TrainerEmployeeId",
                table: "TrainingCourses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingCourses_TrainerEmployeeId",
                table: "TrainingCourses",
                column: "TrainerEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_TrainingCourses_Employees_TrainerEmployeeId",
                table: "TrainingCourses",
                column: "TrainerEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrainingCourses_Employees_TrainerEmployeeId",
                table: "TrainingCourses");

            migrationBuilder.DropIndex(
                name: "IX_TrainingCourses_TrainerEmployeeId",
                table: "TrainingCourses");

            migrationBuilder.DropColumn(
                name: "TrainerEmployeeId",
                table: "TrainingCourses");
        }
    }
}
