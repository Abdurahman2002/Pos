using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsApp2.Migrations
{
    /// <inheritdoc />
    public partial class customerRecipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PosShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerReceipts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReceipts_PosShifts_PosShiftId",
                        column: x => x.PosShiftId,
                        principalTable: "PosShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReceipts_CustomerId_ReceiptDate",
                table: "CustomerReceipts",
                columns: new[] { "CustomerId", "ReceiptDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReceipts_PosShiftId",
                table: "CustomerReceipts",
                column: "PosShiftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerReceipts");
        }
    }
}
