using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace LogiTrack.Models ;

    public class Order
    {
        [Key]
        public int OrderId { get; set; }
        public string CustomerName { get; set; }
        public DateTime DatePlaced { get; set; }
        public List<InventoryItem> OrderList { get; set; } = new List<InventoryItem>();
        
        public Order() {}

        public Order(int orderId, string customerName)
        {
            OrderId = orderId;
            CustomerName = customerName;
            DatePlaced = DateTime.Now;
            OrderList = new List<InventoryItem>();
        }
        
        public void AddItem(InventoryItem item)
        {
            OrderList.Add(item);
        }

        public void AddItems(IEnumerable<InventoryItem> items)
        {
            OrderList.AddRange(items);
        }

        public void RemoveItem(int itemId)
        {
            OrderList.RemoveAll(x => x.ItemID == itemId);
        }

        public void GetOrderSummary()
        {
            Console.WriteLine(  $"Order #{OrderId} for {CustomerName} | Items: {OrderList.Count} | Placed: " +
                                DatePlaced.ToString("M/d/yyyy", CultureInfo.InvariantCulture));
        }
        
    }