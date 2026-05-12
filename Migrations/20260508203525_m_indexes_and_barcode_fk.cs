using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsApp2.Migrations
{
    /// <inheritdoc />
    public partial class m_indexes_and_barcode_fk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BarcodeMappings_Items_ItemId",
                table: "BarcodeMappings");

            migrationBuilder.CreateIndex(
                name: "IX_InvStockLedgers_ReferenceType_ReferenceId",
                table: "InvStockLedgers",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.AddForeignKey(
                name: "FK_BarcodeMappings_Items_ItemId",
                table: "BarcodeMappings",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BarcodeMappings_Items_ItemId",
                table: "BarcodeMappings");

            migrationBuilder.DropIndex(
                name: "IX_InvStockLedgers_ReferenceType_ReferenceId",
                table: "InvStockLedgers");

            migrationBuilder.AddForeignKey(
                name: "FK_BarcodeMappings_Items_ItemId",
                table: "BarcodeMappings",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
