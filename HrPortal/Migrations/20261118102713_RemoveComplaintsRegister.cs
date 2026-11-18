using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class RemoveComplaintsRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Complaints");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Complaints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActionTaken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ClientEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClientPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Nature = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReceivedOn = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RecordedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ResolvedOn = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    StaffInvolved = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Complaints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Complaints_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_CompanyId",
                table: "Complaints",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_CompanyId_ReceivedOn",
                table: "Complaints",
                columns: new[] { "CompanyId", "ReceivedOn" });
        }
    }
}
