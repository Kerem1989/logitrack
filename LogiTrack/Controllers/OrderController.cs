using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LogiTrack.Models;
using LogiTrack.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Controllers ;

    [ApiController]
    [Authorize]
    [Route("api/orders")]
    public class OrderController : ControllerBase
    {
        private readonly LogiTrackContext _logiTrackContext;
        private readonly InventoryCacheSignal _inventoryCacheSignal;

        public OrderController(LogiTrackContext logiTrackContext, InventoryCacheSignal inventoryCacheSignal)
        {
            _logiTrackContext = logiTrackContext;
            _inventoryCacheSignal = inventoryCacheSignal;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetAllOrders(
            [Range(1, int.MaxValue)] int page = 1,
            [Range(1, Paging.MaxPageSize)] int pageSize = Paging.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            var query = VisibleOrders();
            Response.Headers[Paging.TotalCountHeader] = (await query.CountAsync(cancellationToken)).ToString();

            // An empty collection is a valid result, not an error
            return await query
                .OrderBy(o => o.OrderId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderDto(o.OrderId, o.CustomerName, o.DatePlaced))
                .ToListAsync(cancellationToken);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OrderDetailsDto>> GetOrder(int id, CancellationToken cancellationToken)
        {
            // Another user's order is reported as not found, so order IDs can't be probed
            var order = await VisibleOrders()
                .Where(o => o.OrderId == id)
                .Select(o => new OrderDetailsDto(o.OrderId, o.CustomerName, o.DatePlaced,
                    o.OrderList.Select(i => new InventoryItemDto(i.ItemId, i.Name, i.Quantity, i.Location, i.OrderId))
                        .ToList()))
                .FirstOrDefaultAsync(cancellationToken);

            if (order == null)
            {
                return OrderNotFound(id);
            }
            return order;
        }

        [HttpPost]
        public async Task<ActionResult<OrderDetailsDto>> PostOrder([FromBody, MinLength(1)] List<CreateInventoryItemDto> items)
        {
            // [ApiController] returns 400 ProblemDetails automatically for a missing/invalid body
            var order = new Order
            {
                CustomerName = User.Identity!.Name!,
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                DatePlaced = DateTime.UtcNow
            };
            order.AddItems(items.Select(i => new InventoryItem(i.Name, i.Quantity, i.Location)));
            _logiTrackContext.Orders.Add(order);
            await _logiTrackContext.SaveChangesAsync();
            // The order's items are new InventoryItems rows
            _inventoryCacheSignal.Invalidate();
            return new OrderDetailsDto(order.OrderId, order.CustomerName, order.DatePlaced,
                order.OrderList.Select(InventoryItemDto.From).ToList());
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [Authorize(Roles = Roles.Admin)]
        public async Task<ActionResult<OrderDto>> DeleteOrder(int id, CancellationToken cancellationToken)
        {
            var order = await _logiTrackContext.Orders.FindAsync([id], cancellationToken);
            if (order == null)
            {
                return OrderNotFound(id);
            }
            _logiTrackContext.Orders.Remove(order);
            await _logiTrackContext.SaveChangesAsync();
            // Deleting an order cascade-deletes its InventoryItems
            _inventoryCacheSignal.Invalidate();
            return OrderDto.From(order);
        }

        // Admins see every order; everyone else only sees their own
        private IQueryable<Order> VisibleOrders()
        {
            var orders = _logiTrackContext.Orders.AsNoTracking();
            if (User.IsInRole(Roles.Admin))
            {
                return orders;
            }
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return orders.Where(o => o.UserId == userId);
        }

        private ObjectResult OrderNotFound(int id) =>
            Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Order not found",
                detail: $"No order exists with ID {id}.");
    }
