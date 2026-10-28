using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class RemoveApprovalConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // An approver is now whoever holds the sign-in role.
            // Anybody the settings page named gets granted it here,
            // or a company that filled the page in but never granted
            // the role would come out of this migration with nobody
            // able to approve anything and no sign that anything had
            // changed.
            //
            // Somebody named but with no account to sign in with is
            // skipped: there is no role to grant them, and they
            // could not have opened the page either way.
            migrationBuilder.Sql(
                "INSERT INTO [AspNetUserRoles] ([UserId], [RoleId]) " +
                "SELECT e.[UserId], r.[Id] " +
                "FROM [ApprovalConfigurations] a " +
                "INNER JOIN [Employees] e ON e.[Id] = a.[EmployeeId] " +
                "INNER JOIN [AspNetRoles] r ON r.[Name] = " +
                "    CASE a.[Role] " +
                "        WHEN 'HROfficer' THEN 'HR Officer' " +
                "        WHEN 'MD' THEN 'MD' " +
                "    END " +
                "WHERE a.[IsActive] = 1 " +
                "  AND e.[IsActive] = 1 " +
                "  AND e.[UserId] IS NOT NULL " +
                "  AND NOT EXISTS (" +
                "      SELECT 1 FROM [AspNetUserRoles] ur " +
                "      WHERE ur.[UserId] = e.[UserId] " +
                "        AND ur.[RoleId] = r.[Id]);");

            migrationBuilder.DropTable(
                name: "ApprovalConfigurations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The table comes back empty, and the roles granted above
            // stay granted: there is no record of which of them were
            // already held, so taking any of them away would be a
            // guess at somebody's access.
            migrationBuilder.CreateTable(
                name: "ApprovalConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalConfigurations_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalConfigurations_EmployeeId",
                table: "ApprovalConfigurations",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalConfigurations_Role",
                table: "ApprovalConfigurations",
                column: "Role",
                unique: true,
                filter: "[IsActive] = 1");
        }
    }
}
