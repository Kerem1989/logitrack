using LogiTrack.Models;
using Microsoft.EntityFrameworkCore;

class Program
{
    static void Main(string[] args)
    {
        var order = new Order(1001, "Samir");
        order.AddItem(new InventoryItem("Pallet Jack", 12, "Warehouse A") { ItemID = 1 });
        order.AddItem(new InventoryItem("Forklift", 2, "Warehouse B") { ItemID = 2 });
        order.AddItem(new InventoryItem("Shrink Wrap", 40, "Warehouse A") { ItemID = 3 });
        order.RemoveItem(3);
        order.GetOrderSummary();
    }

    // Projects each order to a summary so the item count is computed in SQL; no InventoryItem rows are loaded
    static void PrintOrderSummaries(LogiTrackContext context)
    {
        var summaries = context.Orders
            .AsNoTracking()
            .OrderBy(o => o.DatePlaced)
            .Select(o => new OrderSummary(o.OrderId, o.CustomerName, o.DatePlaced, o.OrderList.Count));

        // Iterate the query directly to stream rows instead of buffering them with ToList()
        foreach (var s in summaries)
        {
            Console.WriteLine($"Order: {s.OrderId} for {s.CustomerName} | Items: {s.ItemCount} | Placed: {s.DatePlaced}");
        }
    }
}

record OrderSummary(int OrderId, string CustomerName, DateTime DatePlaced, int ItemCount);

/*
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();

    var app = builder.Build();

// Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();



    app.Run();
    */

