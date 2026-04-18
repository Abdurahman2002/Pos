using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NewsApp2.Models;

#nullable disable

namespace NewsApp2.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260412183000_AddSalesInvoiceDrafts")]
    public partial class AddSalesInvoiceDrafts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesInvoiceDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    EurToDinarRateSnapshot = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    DiscountType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsReturn = table.Column<bool>(type: "bit", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerNameSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubtotalEur = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountEur = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetEur = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetDinar = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesInvoiceDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesInvoiceDrafts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceDrafts_CreatedByUserId",
                table: "SalesInvoiceDrafts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceDrafts_CustomerId",
                table: "SalesInvoiceDrafts",
                column: "CustomerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesInvoiceDrafts");
        }
    }
}