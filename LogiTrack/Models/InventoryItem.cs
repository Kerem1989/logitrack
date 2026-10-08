using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models ;

    public class InventoryItem
    {
        [Key]
        public int ItemID {get; set;}
        public string Name {get; set;}
        public int Quantity {get; set;}
        public string Location {get; set;}

        // Foreign key + navigation back to the owning Order (nullable: items can exist in stock without an order)
        public int? OrderId {get; set;}
        public Order? Order {get; set;}

        public InventoryItem() {}

        public InventoryItem(string name, int quantity, string location)
        {
            Name = name;
            Quantity = quantity;
            Location = location;
        }

        public void DisplayInfo()
        {
            Console.WriteLine($"Item: {Name} |  Quantity: {Quantity} | Location: {Location}");
        }
    }