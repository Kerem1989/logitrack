using System.Diagnostics;
using LogiTrack.Models;
using LogiTrack.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LogiTrack.Controllers ;

    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly LogiTrackContext _logiTrackContext;
        private readonly IMemoryCache _cache;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(LogiTrackContext logiTrackContext, IMemoryCache cache,
            ILogger<InventoryController> logger)
        {
            _logiTrackContext = logiTrackContext;
            _cache = cache;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryItem>>> GetAllInventoryItems(CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();

            // Repeated reads are served from memory; the writes below evict the entry so it is never stale
            // for changes made through this API. The expiry is a backstop for changes made outside it.
            var items = await _cache.GetOrCreateAsync(CacheKeys.AllInventory, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                _logger.LogInformation("Inventory cache miss, loading from database");
                return _logiTrackContext.InventoryItems.AsNoTracking().ToListAsync(cancellationToken);
            });
            // An empty collection is a valid result, not an error
            sw.Stop();
            _logger.LogInformation("GET /api/inventory took {ElapsedMs} ms", sw.Elapsed.TotalMilliseconds);
            return items!;
        }

        [HttpPost]
        public async Task<ActionResult<InventoryItem>> PostInventoryItem([FromBody] InventoryItem inventoryItem)
        {
            if (inventoryItem.Name == null || inventoryItem.Quantity == 0 || inventoryItem.Location == null)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid inventory item",
                    detail: "Name, Location and a non-zero Quantity are required.");
            }
            _logiTrackContext.Add(inventoryItem);
            await _logiTrackContext.SaveChangesAsync();
            _cache.Remove(CacheKeys.AllInventory);
            return inventoryItem;
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<InventoryItem>> DeleteInventoryItem(int id, CancellationToken cancellationToken)
        {
            InventoryItem? inventoryItem = await _logiTrackContext.InventoryItems.FindAsync([id], cancellationToken);
            if (inventoryItem == null)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Inventory item not found",
                    detail: $"No inventory item exists with ID {id}.");
            }
            _logiTrackContext.Remove(inventoryItem);
            await _logiTrackContext.SaveChangesAsync();
            _cache.Remove(CacheKeys.AllInventory);
            return inventoryItem;
        }
    }
