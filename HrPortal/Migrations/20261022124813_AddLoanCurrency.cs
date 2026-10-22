using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "LoanApplications",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                // existing loans predate the choice and were all in
                // US dollars, which is what the enum falls back to
                defaultValue: "Usd");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "LoanApplications");
        }
    }
}
