using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsApp2.Migrations
{
    /// <inheritdoc />
    public partial class AddMovingAverageCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LineCostDinar",
                table: "SalesLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCostLyd",
                table: "SalesLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCostLyd",
                table: "PurchaseLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCostLyd",
                table: "InvStockLedgers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageCostLyd",
                table: "InvStockBalances",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LineCostDinar",
                table: "SalesLines");

            migrationBuilder.DropColumn(
                name: "UnitCostLyd",
                table: "SalesLines");

            migrationBuilder.DropColumn(
                name: "UnitCostLyd",
                table: "PurchaseLines");

            migrationBuilder.DropColumn(
                name: "UnitCostLyd",
                table: "InvStockLedgers");

            migrationBuilder.DropColumn(
                name: "AverageCostLyd",
                table: "InvStockBalances");
        }
    }
}
