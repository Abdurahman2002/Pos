using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsApp2.Migrations
{
    /// <inheritdoc />
    public partial class gl_journal_for_purchases_sales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinJournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AccountCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinJournalEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinJournalEntries_EntryDate_AccountCode",
                table: "FinJournalEntries",
                columns: new[] { "EntryDate", "AccountCode" });

            migrationBuilder.CreateIndex(
                name: "IX_FinJournalEntries_SourceType_SourceId",
                table: "FinJournalEntries",
                columns: new[] { "SourceType", "SourceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinJournalEntries");
        }
    }
}
