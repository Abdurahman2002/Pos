using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NewsApp2.Models;

#nullable disable

namespace NewsApp2.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260613120000_PurchaseDiscountsAndAccountBalances")]
    public partial class PurchaseDiscountsAndAccountBalances : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ValueChangeLyd",
                table: "InvStockLedgers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalEur",
                table: "PurchaseInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalDinar",
                table: "PurchaseInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountType",
                table: "PurchaseInvoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Amount");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "PurchaseInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountEur",
                table: "PurchaseInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountDinar",
                table: "PurchaseInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAllocatedEur",
                table: "PurchaseLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAllocatedDinar",
                table: "PurchaseLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE PurchaseInvoices
                SET SubtotalEur = TotalEur,
                    SubtotalDinar = TotalDinar,
                    DiscountType = N'Amount',
                    DiscountValue = 0,
                    DiscountEur = 0,
                    DiscountDinar = 0;
                """);

            migrationBuilder.Sql("""
                UPDATE InvStockLedgers
                SET ValueChangeLyd = QuantityChange * UnitCostLyd;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValueChangeLyd",
                table: "InvStockLedgers");

            migrationBuilder.DropColumn(
                name: "SubtotalEur",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "SubtotalDinar",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "DiscountEur",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "DiscountDinar",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "DiscountAllocatedEur",
                table: "PurchaseLines");

            migrationBuilder.DropColumn(
                name: "DiscountAllocatedDinar",
                table: "PurchaseLines");
        }
    }
}
