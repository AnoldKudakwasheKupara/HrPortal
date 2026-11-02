using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class SanctionsCorrectionsAndReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CorrectsReviewId",
                table: "SanctionsReviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PreviousChosen",
                table: "SanctionsReviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsReviews_CorrectsReviewId",
                table: "SanctionsReviews",
                column: "CorrectsReviewId");

            migrationBuilder.AddForeignKey(
                name: "FK_SanctionsReviews_SanctionsReviews_CorrectsReviewId",
                table: "SanctionsReviews",
                column: "CorrectsReviewId",
                principalTable: "SanctionsReviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SanctionsReviews_SanctionsReviews_CorrectsReviewId",
                table: "SanctionsReviews");

            migrationBuilder.DropIndex(
                name: "IX_SanctionsReviews_CorrectsReviewId",
                table: "SanctionsReviews");

            migrationBuilder.DropColumn(
                name: "CorrectsReviewId",
                table: "SanctionsReviews");

            migrationBuilder.DropColumn(
                name: "PreviousChosen",
                table: "SanctionsReviews");
        }
    }
}
