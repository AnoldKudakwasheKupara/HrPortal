using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            foreach (var (table, links) in new[]
            {
                ("LeaveRequests", "LeaveApproval|LeaveRequest|MyLeave"),
                ("LoanApplications", "LoanApproval|MyLoans"),
                ("CourseApplications", "CourseApproval|MyCourses")
            })
            {
                migrationBuilder.Sql($"""
                    UPDATE "Notifications" n
                    SET "CompanyId" = e."HomeCompanyId"
                    FROM "{table}" r
                    JOIN "Employees" e ON e."Id" = r."EmployeeId"
                    WHERE n."CompanyId" IS NULL
                      AND n."Link" ~ '^/({links})/(Details|Edit)/[0-9]+$'
                      AND r."Id" = substring(n."Link" from '[0-9]+$')::int;
                    """);
            }

            migrationBuilder.Sql("""
                UPDATE "Notifications" n
                SET "CompanyId" = held."CompanyId"
                FROM (
                    SELECT e."Id" AS "EmployeeId", min(cr."CompanyId") AS "CompanyId"
                    FROM "CompanyRoles" cr
                    JOIN "Employees" e ON e."UserId" = cr."UserId"
                    GROUP BY e."Id"
                    HAVING count(DISTINCT cr."CompanyId") = 1
                ) held
                WHERE n."CompanyId" IS NULL
                  AND n."EmployeeId" = held."EmployeeId"
                  AND n."Link" IN ('/LeaveApproval', '/LeaveApproval/FinalApproval', '/CourseApproval/Approved');
                """);

            migrationBuilder.Sql("""
                UPDATE "Notifications" n
                SET "CompanyId" = single."CompanyId"
                FROM (
                    SELECT "EmployeeId", min("CompanyId") AS "CompanyId"
                    FROM "EmployeeCompanies"
                    GROUP BY "EmployeeId"
                    HAVING count(*) = 1
                ) single
                WHERE n."CompanyId" IS NULL
                  AND n."Link" IS NOT NULL
                  AND n."EmployeeId" = single."EmployeeId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Notifications");
        }
    }
}
