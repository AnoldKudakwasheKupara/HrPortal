using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderForms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Member = table.Column<string>(type: "text", nullable: false),
                    Employer = table.Column<string>(type: "text", nullable: false),
                    Signature = table.Column<byte[]>(type: "bytea", nullable: true),
                    WitnessSignature = table.Column<byte[]>(type: "bytea", nullable: true),
                    EmployerSignature = table.Column<byte[]>(type: "bytea", nullable: true),
                    ReturnComment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CompletedByEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderForms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderForms_Employees_CompletedByEmployeeId",
                        column: x => x.CompletedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProviderForms_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderForms_CompletedByEmployeeId",
                table: "ProviderForms",
                column: "CompletedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderForms_EmployeeId_Status",
                table: "ProviderForms",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderForms_Reference",
                table: "ProviderForms",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderForms_Status",
                table: "ProviderForms",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderForms");
        }
    }
}
