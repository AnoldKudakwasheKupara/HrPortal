using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordVault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VaultEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EncryptedPassword = table.Column<string>(type: "text", nullable: false),
                    EncryptedNotes = table.Column<string>(type: "text", nullable: true),
                    PasswordChangedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UpdatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaultEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VaultEntries_Name",
                table: "VaultEntries",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VaultEntries");
        }
    }
}
