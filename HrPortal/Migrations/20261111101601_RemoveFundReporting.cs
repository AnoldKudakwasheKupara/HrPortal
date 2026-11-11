using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HrPortal.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFundReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FundFxRates");

            migrationBuilder.DropTable(
                name: "FundMarketQuarters");

            migrationBuilder.DropTable(
                name: "FundPolicyLines");

            migrationBuilder.DropTable(
                name: "FundPositions");

            migrationBuilder.DropTable(
                name: "FundSecurities");

            migrationBuilder.DropTable(
                name: "FundStatementBalances");

            migrationBuilder.DropTable(
                name: "FundStatementImports");

            migrationBuilder.DropTable(
                name: "FundTransactionRules");

            migrationBuilder.DropTable(
                name: "FundTransactions");

            migrationBuilder.DropTable(
                name: "FundAccounts");

            migrationBuilder.DropTable(
                name: "PensionFunds");

            migrationBuilder.Sql("""
                DELETE FROM "AspNetUserRoles"
                WHERE "RoleId" IN (SELECT "Id" FROM "AspNetRoles" WHERE "Name" = 'Investment Analyst');

                DELETE FROM "AspNetRoleClaims"
                WHERE "RoleId" IN (SELECT "Id" FROM "AspNetRoles" WHERE "Name" = 'Investment Analyst');

                DELETE FROM "AspNetRoles" WHERE "Name" = 'Investment Analyst';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FundFxRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Month = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ZwgPerUsd = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundFxRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FundMarketQuarters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Bonds = table.Column<decimal>(type: "numeric", nullable: false),
                    Cash = table.Column<decimal>(type: "numeric", nullable: false),
                    MoneyMarket = table.Column<decimal>(type: "numeric", nullable: false),
                    Quarter = table.Column<int>(type: "integer", nullable: false),
                    UsdInflation = table.Column<decimal>(type: "numeric", nullable: false),
                    VfexAllShare = table.Column<decimal>(type: "numeric", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    ZseAllShareUsd = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundMarketQuarters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FundSecurities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetClass = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConfirmedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LargeCap = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Prescribed = table.Column<bool>(type: "boolean", nullable: false),
                    SecurityCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundSecurities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FundStatementImports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PensionFundId = table.Column<int>(type: "integer", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UploadedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundStatementImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FundTransactionRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DecidedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DecidedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Manager = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Treatment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundTransactionRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PensionFunds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PensionFunds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FundAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PensionFundId = table.Column<int>(type: "integer", nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClosedFromMonth = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Manager = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubFund = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundAccounts_PensionFunds_PensionFundId",
                        column: x => x.PensionFundId,
                        principalTable: "PensionFunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FundPolicyLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PensionFundId = table.Column<int>(type: "integer", nullable: false),
                    AssetClass = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CorridorHigh = table.Column<decimal>(type: "numeric", nullable: false),
                    CorridorLow = table.Column<decimal>(type: "numeric", nullable: false),
                    IpecLimit = table.Column<decimal>(type: "numeric", nullable: true),
                    Target = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundPolicyLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundPolicyLines_PensionFunds_PensionFundId",
                        column: x => x.PensionFundId,
                        principalTable: "PensionFunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FundPositions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FundAccountId = table.Column<int>(type: "integer", nullable: false),
                    AccruedInterest = table.Column<decimal>(type: "numeric", nullable: true),
                    BookCost = table.Column<decimal>(type: "numeric", nullable: true),
                    Confidence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MarketValue = table.Column<decimal>(type: "numeric", nullable: false),
                    MaturityDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Month = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RatePercent = table.Column<decimal>(type: "numeric", nullable: true),
                    SecurityCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SecurityName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceFile = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SourceSection = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Units = table.Column<decimal>(type: "numeric", nullable: true),
                    ValueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundPositions_FundAccounts_FundAccountId",
                        column: x => x.FundAccountId,
                        principalTable: "FundAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FundStatementBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FundAccountId = table.Column<int>(type: "integer", nullable: false),
                    ClosingCash = table.Column<decimal>(type: "numeric", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Month = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    OpeningCash = table.Column<decimal>(type: "numeric", nullable: true),
                    PrintedTotal = table.Column<decimal>(type: "numeric", nullable: true),
                    SourceFile = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundStatementBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundStatementBalances_FundAccounts_FundAccountId",
                        column: x => x.FundAccountId,
                        principalTable: "FundAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FundTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FundAccountId = table.Column<int>(type: "integer", nullable: false),
                    CashIn = table.Column<bool>(type: "boolean", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Fees = table.Column<decimal>(type: "numeric", nullable: true),
                    GrossAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    LedgerDelta = table.Column<decimal>(type: "numeric", nullable: false),
                    Month = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: true),
                    SecurityCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SecurityName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SourceFile = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SourceLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Treatment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TxnDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TxnType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Units = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundTransactions_FundAccounts_FundAccountId",
                        column: x => x.FundAccountId,
                        principalTable: "FundAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FundAccounts_PensionFundId_Manager_AccountNumber",
                table: "FundAccounts",
                columns: new[] { "PensionFundId", "Manager", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundFxRates_Month",
                table: "FundFxRates",
                column: "Month",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundMarketQuarters_Year_Quarter",
                table: "FundMarketQuarters",
                columns: new[] { "Year", "Quarter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundPolicyLines_PensionFundId_AssetClass",
                table: "FundPolicyLines",
                columns: new[] { "PensionFundId", "AssetClass" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundPositions_FundAccountId_Month",
                table: "FundPositions",
                columns: new[] { "FundAccountId", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_FundSecurities_SecurityCode",
                table: "FundSecurities",
                column: "SecurityCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundStatementBalances_FundAccountId_Month_Currency",
                table: "FundStatementBalances",
                columns: new[] { "FundAccountId", "Month", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_FundStatementImports_PensionFundId",
                table: "FundStatementImports",
                column: "PensionFundId");

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactionRules_Manager_Label",
                table: "FundTransactionRules",
                columns: new[] { "Manager", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactions_FundAccountId_Month",
                table: "FundTransactions",
                columns: new[] { "FundAccountId", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_FundTransactions_Treatment",
                table: "FundTransactions",
                column: "Treatment");

            migrationBuilder.CreateIndex(
                name: "IX_PensionFunds_Name",
                table: "PensionFunds",
                column: "Name",
                unique: true);
        }
    }
}
