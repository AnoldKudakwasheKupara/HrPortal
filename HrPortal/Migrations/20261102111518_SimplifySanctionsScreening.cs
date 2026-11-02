using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class SimplifySanctionsScreening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"SanctionsConcerns\"; DELETE FROM \"SanctionsChanges\";");

            migrationBuilder.DropForeignKey(
                name: "FK_SanctionsChanges_SanctionsLists_SanctionsListId",
                table: "SanctionsChanges");

            migrationBuilder.DropForeignKey(
                name: "FK_SanctionsConcerns_SanctionsLists_SanctionsListId",
                table: "SanctionsConcerns");

            migrationBuilder.DropTable(
                name: "SanctionsListEntries");

            migrationBuilder.DropTable(
                name: "SanctionsRegister");

            migrationBuilder.DropTable(
                name: "SanctionsLists");

            migrationBuilder.RenameColumn(
                name: "SanctionsListId",
                table: "SanctionsConcerns",
                newName: "SanctionsReviewId");

            migrationBuilder.RenameIndex(
                name: "IX_SanctionsConcerns_SanctionsListId",
                table: "SanctionsConcerns",
                newName: "IX_SanctionsConcerns_SanctionsReviewId");

            migrationBuilder.RenameColumn(
                name: "SanctionsListId",
                table: "SanctionsChanges",
                newName: "SanctionsReviewId");

            migrationBuilder.RenameIndex(
                name: "IX_SanctionsChanges_SanctionsListId",
                table: "SanctionsChanges",
                newName: "IX_SanctionsChanges_SanctionsReviewId");

            migrationBuilder.AddColumn<string>(
                name: "EntryFingerprint",
                table: "SanctionsConcerns",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SanctionsDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DocumentReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IssuedOn = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UploadedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LooksLikeNotice = table.Column<bool>(type: "boolean", nullable: false),
                    ReadingWarnings = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanctionsDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SanctionsEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SanctionsDocumentId = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsEntity = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Aliases = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Designation = table.Column<string>(type: "text", nullable: true),
                    DateOfBirth = table.Column<string>(type: "text", nullable: true),
                    PlaceOfBirth = table.Column<string>(type: "text", nullable: true),
                    Nationality = table.Column<string>(type: "text", nullable: true),
                    Identifiers = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    ListedOn = table.Column<string>(type: "text", nullable: true),
                    OtherInformation = table.Column<string>(type: "text", nullable: true),
                    Details = table.Column<string>(type: "text", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanctionsEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SanctionsEntries_SanctionsDocuments_SanctionsDocumentId",
                        column: x => x.SanctionsDocumentId,
                        principalTable: "SanctionsDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SanctionsReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NewDocumentId = table.Column<int>(type: "integer", nullable: false),
                    PreviousDocumentId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Screened = table.Column<bool>(type: "boolean", nullable: false),
                    ScreenedStaff = table.Column<int>(type: "integer", nullable: false),
                    ScreenedVendors = table.Column<int>(type: "integer", nullable: false),
                    ScreenedClients = table.Column<int>(type: "integer", nullable: false),
                    ClientListId = table.Column<int>(type: "integer", nullable: true),
                    FinalisedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FinalisedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReportPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReportSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanctionsReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SanctionsReviews_SanctionsClientLists_ClientListId",
                        column: x => x.ClientListId,
                        principalTable: "SanctionsClientLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SanctionsReviews_SanctionsDocuments_NewDocumentId",
                        column: x => x.NewDocumentId,
                        principalTable: "SanctionsDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SanctionsReviews_SanctionsDocuments_PreviousDocumentId",
                        column: x => x.PreviousDocumentId,
                        principalTable: "SanctionsDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsDocuments_UploadedAt",
                table: "SanctionsDocuments",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsEntries_SanctionsDocumentId_Reference",
                table: "SanctionsEntries",
                columns: new[] { "SanctionsDocumentId", "Reference" });

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsReviews_ClientListId",
                table: "SanctionsReviews",
                column: "ClientListId");

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsReviews_NewDocumentId",
                table: "SanctionsReviews",
                column: "NewDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsReviews_PreviousDocumentId",
                table: "SanctionsReviews",
                column: "PreviousDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsReviews_ReportSha256",
                table: "SanctionsReviews",
                column: "ReportSha256");

            migrationBuilder.CreateIndex(
                name: "IX_SanctionsReviews_ReviewNumber",
                table: "SanctionsReviews",
                column: "ReviewNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SanctionsChanges_SanctionsReviews_SanctionsReviewId",
                table: "SanctionsChanges",
                column: "SanctionsReviewId",
                principalTable: "SanctionsReviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SanctionsConcerns_SanctionsReviews_SanctionsReviewId",
                table: "SanctionsConcerns",
                column: "SanctionsReviewId",
                principalTable: "SanctionsReviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SanctionsChanges_SanctionsReviews_SanctionsReviewId",
                table: "SanctionsChanges");

            migrationBuilder.DropForeignKey(
                name: "FK_SanctionsConcerns_SanctionsReviews_SanctionsReviewId",
                table: "SanctionsConcerns");

            migrationBuilder.DropTable(
                name: "SanctionsEntries");

            migrationBuilder.DropTable(
                name: "SanctionsReviews");

            migrationBuilder.DropTable(
                name: "SanctionsDocuments");

            migrationBuilder.DropColumn(
                name: "EntryFingerprint",
                table: "SanctionsConcerns");

            migrationBuilder.RenameColumn(
                name: "SanctionsReviewId",
                table: "SanctionsConcerns",
                newName: "SanctionsListId");

            migrationBuilder.RenameIndex(
                name: "IX_SanctionsConcerns_SanctionsReviewId",
                table: "SanctionsConcerns",
                newName: "IX_SanctionsConcerns_SanctionsListId");

            migrationBuilder.RenameColumn(
                name: "SanctionsReviewId",
                table: "SanctionsChanges",
                newName: "SanctionsListId");

            migrationBuilder.RenameIndex(
                name: "IX_SanctionsChanges_SanctionsReviewId",
                table: "SanctionsChanges",
                newName: "IX_SanctionsChanges_SanctionsListId");

            migrationBuilder.CreateTable(
                name: "SanctionsLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientListId = table.Column<int>(type: "integer", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AppliedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DocumentReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IssuedOn = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RegisterSizeAfter = table.Column<int>(type: "integer", nullable: false),
                    ReportPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReportSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReviewNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScreenedClients = table.Column<int>(type: "integer", nullable: false),
                    ScreenedStaff = table.Column<int>(type: "integer", nullable: false),
                    ScreenedVendors = table.Column<int>(type: "integer", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SignOffNote = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SignedOffAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SignedOffByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UploadedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
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
                name: "SanctionsRegister",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Aliases = table.Column<string>(type: "text", nullable: true),
                    DateOfBirth = table.Column<string>(type: "text", nullable: true),
                    Designation = table.Column<string>(type: "text", nullable: true),
                    Details = table.Column<string>(type: "text", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FirstListId = table.Column<int>(type: "integer", nullable: false),
                    Identifiers = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsEntity = table.Column<bool>(type: "boolean", nullable: false),
                    LastListId = table.Column<int>(type: "integer", nullable: false),
                    ListedOn = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Nationality = table.Column<string>(type: "text", nullable: true),
                    OtherInformation = table.Column<string>(type: "text", nullable: true),
                    PlaceOfBirth = table.Column<string>(type: "text", nullable: true),
                    Reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanctionsRegister", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SanctionsListEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SanctionsListId = table.Column<int>(type: "integer", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Aliases = table.Column<string>(type: "text", nullable: true),
                    DateOfBirth = table.Column<string>(type: "text", nullable: true),
                    Designation = table.Column<string>(type: "text", nullable: true),
                    Details = table.Column<string>(type: "text", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Identifiers = table.Column<string>(type: "text", nullable: true),
                    IsEntity = table.Column<bool>(type: "boolean", nullable: false),
                    ListedOn = table.Column<string>(type: "text", nullable: true),
                    MarkedRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Nationality = table.Column<string>(type: "text", nullable: true),
                    OtherInformation = table.Column<string>(type: "text", nullable: true),
                    PlaceOfBirth = table.Column<string>(type: "text", nullable: true),
                    Reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true)
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

            migrationBuilder.AddForeignKey(
                name: "FK_SanctionsChanges_SanctionsLists_SanctionsListId",
                table: "SanctionsChanges",
                column: "SanctionsListId",
                principalTable: "SanctionsLists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SanctionsConcerns_SanctionsLists_SanctionsListId",
                table: "SanctionsConcerns",
                column: "SanctionsListId",
                principalTable: "SanctionsLists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
