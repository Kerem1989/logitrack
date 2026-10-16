using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogiTrack.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_Orders_OrderId",
                table: "InventoryItems");

            migrationBuilder.RenameColumn(
                name: "ItemID",
                table: "InventoryItems",
                newName: "ItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_Orders_OrderId",
                table: "InventoryItems",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "OrderId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_Orders_OrderId",
                table: "InventoryItems");

            migrationBuilder.RenameColumn(
                name: "ItemId",
                table: "InventoryItems",
                newName: "ItemID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_Orders_OrderId",
                table: "InventoryItems",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "OrderId",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
