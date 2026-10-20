using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models ;

    // Request body for new items: only client-settable fields, so ItemId/OrderId can't be overposted
    public class CreateInventoryItemDto
    {
        [Required] public string Name { get; set; } = "";
        [Range(1, int.MaxValue)] public int Quantity { get; set; }
        [Required] public string Location { get; set; } = "";
    }

    public record InventoryItemDto(int ItemId, string Name, int Quantity, string Location, int? OrderId)
    {
        public static InventoryItemDto From(InventoryItem item) =>
            new(item.ItemId, item.Name, item.Quantity, item.Location, item.OrderId);
    }

    public record OrderDto(int OrderId, string CustomerName, DateTime DatePlaced)
    {
        public static OrderDto From(Order order) => new(order.OrderId, order.CustomerName, order.DatePlaced);
    }

    public record OrderDetailsDto(int OrderId, string CustomerName, DateTime DatePlaced, List<InventoryItemDto> Items);
