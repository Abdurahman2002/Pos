using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsApp2.Migrations
{
    /// <inheritdoc />
    public partial class supplier_payment_voucher_decoupled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPayments_PurchaseInvoices_PurchaseInvoiceId",
                table: "SupplierPayments");

            migrationBuilder.DropIndex(
                name: "IX_SupplierPayments_PurchaseInvoiceId",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceId",
                table: "SupplierPayments");

            migrationBuilder.AddColumn<string>(
                name: "Number",
                table: "SupplierPayments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(@"
WITH Numbered AS (
    SELECT [Id], ROW_NUMBER() OVER (ORDER BY [PaymentDate], [Created], [Id]) AS [Rn]
    FROM [SupplierPayments]
)
UPDATE sp
SET [Number] = CONCAT('SP-MIG-', RIGHT('000000' + CAST(n.[Rn] AS varchar(6)), 6))
FROM [SupplierPayments] sp
INNER JOIN Numbered n ON n.[Id] = sp.[Id]
WHERE sp.[Number] IS NULL OR LTRIM(RTRIM(sp.[Number])) = '';
");

            migrationBuilder.AlterColumn<string>(
                name: "Number",
                table: "SupplierPayments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_Number",
                table: "SupplierPayments",
                column: "Number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupplierPayments_Number",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "SupplierPayments");

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseInvoiceId",
                table: "SupplierPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_PurchaseInvoiceId",
                table: "SupplierPayments",
                column: "PurchaseInvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPayments_PurchaseInvoices_PurchaseInvoiceId",
                table: "SupplierPayments",
                column: "PurchaseInvoiceId",
                principalTable: "PurchaseInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
