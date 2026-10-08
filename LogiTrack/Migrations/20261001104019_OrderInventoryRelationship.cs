using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogiTrack.Migrations
{
    /// <inheritdoc />
    public partial class OrderInventoryRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_Orders_OrderId",
                table: "InventoryItems");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_Orders_OrderId",
                table: "InventoryItems",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "OrderId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_Orders_OrderId",
                table: "InventoryItems");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_Orders_OrderId",
                table: "InventoryItems",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "OrderId");
        }
    }
}
