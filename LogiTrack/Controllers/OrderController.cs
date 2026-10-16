using LogiTrack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Controllers ;

    [ApiController]
    [Route("api/orders")]
    public class OrderController : ControllerBase
    {
        private readonly LogiTrackContext _logiTrackContext;

        public OrderController(LogiTrackContext logiTrackContext)
        {
            _logiTrackContext = logiTrackContext;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetAllOrders(CancellationToken cancellationToken)
        {
            // An empty collection is a valid result, not an error
            return await _logiTrackContext.Orders.ToListAsync(cancellationToken);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOrder(int id, CancellationToken cancellationToken)
        {
            // Project into a flat shape: OrderList is [JsonIgnore] on the entity, and
            // InventoryItem.Order would create a serialization cycle.
            var order = await _logiTrackContext.Orders
                .Where(o => o.OrderId == id)
                .Select(o => new
                {
                    o.OrderId,
                    o.CustomerName,
                    o.DatePlaced,
                    Items = o.OrderList.Select(i => new
                    {
                        i.ItemId,
                        i.Name,
                        i.Quantity,
                        i.Location
                    }).ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (order == null)
            {
                return OrderNotFound(id);
            }
            return Ok(order);
        }

        [HttpPost]
        public async Task<ActionResult<Order>> PostOrder([FromBody] IEnumerable<InventoryItem> items)
        {
            // [ApiController] returns 400 ProblemDetails automatically for a missing/invalid body
            var order = new Order();
            order.OrderList.AddRange(items);
            order.DatePlaced = DateTime.Now;
            order.CustomerName = "John Doe";
            _logiTrackContext.Orders.Add(order);
            await _logiTrackContext.SaveChangesAsync();
            return order;
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Order>> DeleteOrder(int id, CancellationToken cancellationToken)
        {
            var order = await _logiTrackContext.Orders.FindAsync([id], cancellationToken);
            if (order == null)
            {
                return OrderNotFound(id);
            }
            _logiTrackContext.Orders.Remove(order);
            await _logiTrackContext.SaveChangesAsync();
            return order;
        }

        private ObjectResult OrderNotFound(int id) =>
            Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Order not found",
                detail: $"No order exists with ID {id}.");
    }