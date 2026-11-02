using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddSanctionsScreening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SanctionsClientLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UploadedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanctionsClientLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SanctionsRegister",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsEntity = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Aliases = table.Column<string>(type: "text", nullable: true),
                    DateOfBirth = table.Column<string>(type: "text", nullable: true),
                    Nationality = table.Column<string>(type: "text", nullable: true),
                    Identifiers = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Designation = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: true),
                    PlaceOfBirth = table.Column<string>(type: "text", nullable: true),
                    ListedOn = table.Column<string>(type: "text", nullable: true),
                    OtherInformation = table.Column<string>(type: "text", nullable: true),
                    Details = table.Column<string>(type: "text", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FirstListId = table.Column<int>(type: "integer", nullable: false),
                    LastListId = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanctionsRegister", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SanctionsClients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SanctionsClientListId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IdNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanctionsClients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SanctionsClients_SanctionsClientLists_SanctionsClientListId",
                        column: x => x.SanctionsClientListId,
                        principalTable: "SanctionsClientLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SanctionsLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DocumentReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IssuedOn = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UploadedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AppliedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ScreenedStaff = table.Column<int>(type: "integer", nullable: false),
                    ScreenedVendors = table.Column<int>(type: "integer", nullable: false),
                    ScreenedClients = table.Column<int>(type: "integer", nullable: false),
                    ClientListId = table.Column<int>(type: "integer", nullable: true),
                    RegisterSizeAfter = table.Column<int>(type: "integer", nullable: false),
                    SignedOffAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SignedOffByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SignOffNote = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReportPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReportSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanctionsLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SanctionsLists_SanctionsClientLists_ClientListId",
                        column: x => x.ClientListId,
                        principalTable: "SanctionsClientLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SanctionsChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SanctionsListId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Differences = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanctionsChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SanctionsChanges_SanctionsLists_SanctionsListId",
                        column: x => x.SanctionsListId,
                        principalTable: "SanctionsLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SanctionsConcerns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SanctionsListId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ListedName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SubjectType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SubjectId = table.Column<int>(type: "integer", nullable: true),
                    SubjectName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    RaisedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Resolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResolvedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanctionsConcerns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SanctionsConcerns_SanctionsLists_SanctionsListId",
                        column: x => x.SanctionsListId,
                        principalTable: "SanctionsLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SanctionsListEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SanctionsListId = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsEntity = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Aliases = table.Column<string>(type: "text", nullable: true),
                    DateOfBirth = table.Column<string>(type: "text", nullable: true),
                    Nationality = table.Column<string>(type: "text", nullable: true),
                    Identifiers = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Designation = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: true),
                    PlaceOfBirth = table.Column<string>(type: "text", nullable: true),
                    ListedOn = table.Column<string>(type: "text", nullable: true),
                    OtherInformation = table.Column<string>(type: "text", nullable: true),
                    Details = table.Column<string>(type: "text", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MarkedRemoved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanctionsListEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SanctionsListEntries_SanctionsLists_SanctionsListId",
                        column: x => x.SanctionsListId,
                        principalTable: "SanctionsLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsChanges_SanctionsListId",
                table: "SanctionsChanges",
                column: "SanctionsListId");

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsClients_SanctionsClientListId",
                table: "SanctionsClients",
                column: "SanctionsClientListId");

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsConcerns_SanctionsListId",
                table: "SanctionsConcerns",
                column: "SanctionsListId");

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsListEntries_SanctionsListId_Reference",
                table: "SanctionsListEntries",
                columns: new[] { "SanctionsListId", "Reference" });

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsLists_ClientListId",
                table: "SanctionsLists",
                column: "ClientListId");

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsLists_ReportSha256",
                table: "SanctionsLists",
                column: "ReportSha256");

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsLists_ReviewNumber",
                table: "SanctionsLists",
                column: "ReviewNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsRegister_Reference",
                table: "SanctionsRegister",
                column: "Reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SanctionsChanges");

            migrationBuilder.DropTable(
                name: "SanctionsClients");

            migrationBuilder.DropTable(
                name: "SanctionsConcerns");

            migrationBuilder.DropTable(
                name: "SanctionsListEntries");

            migrationBuilder.DropTable(
                name: "SanctionsRegister");

            migrationBuilder.DropTable(
                name: "SanctionsLists");

            migrationBuilder.DropTable(
                name: "SanctionsClientLists");
        }
    }
}
