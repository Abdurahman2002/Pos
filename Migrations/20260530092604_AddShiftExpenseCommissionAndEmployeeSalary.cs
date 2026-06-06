using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsApp2.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftExpenseCommissionAndEmployeeSalary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CommissionAmountPerStepLyd",
                table: "InventorySettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 10m);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionSalesStepLyd",
                table: "InventorySettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 1000m);

            migrationBuilder.AddColumn<Guid>(
                name: "PosShiftId",
                table: "ExpenseEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseSalaryLyd",
                table: "Employees",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "InventorySettings",
                keyColumn: "Id",
                keyValue: new Guid("d8ec402f-4f11-4f5e-97ed-c9dc2a58a232"),
                columns: new[] { "CommissionAmountPerStepLyd", "CommissionSalesStepLyd" },
                values: new object[] { 10m, 1000m });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseEntries_PosShiftId",
                table: "ExpenseEntries",
                column: "PosShiftId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseEntries_PosShifts_PosShiftId",
                table: "ExpenseEntries",
                column: "PosShiftId",
                principalTable: "PosShifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseEntries_PosShifts_PosShiftId",
                table: "ExpenseEntries");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseEntries_PosShiftId",
                table: "ExpenseEntries");

            migrationBuilder.DropColumn(
                name: "CommissionAmountPerStepLyd",
                table: "InventorySettings");

            migrationBuilder.DropColumn(
                name: "CommissionSalesStepLyd",
                table: "InventorySettings");

            migrationBuilder.DropColumn(
                name: "PosShiftId",
                table: "ExpenseEntries");

            migrationBuilder.DropColumn(
                name: "BaseSalaryLyd",
                table: "Employees");
        }
    }
}
