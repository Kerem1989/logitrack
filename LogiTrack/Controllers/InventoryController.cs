using System.ComponentModel.DataAnnotations;
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
        private readonly InventoryCacheSignal _cacheSignal;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(LogiTrackContext logiTrackContext, IMemoryCache cache,
            InventoryCacheSignal cacheSignal, ILogger<InventoryController> logger)
        {
            _logiTrackContext = logiTrackContext;
            _cache = cache;
            _cacheSignal = cacheSignal;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetAllInventoryItems(
            [Range(1, int.MaxValue)] int page = 1,
            [Range(1, Paging.MaxPageSize)] int pageSize = Paging.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            // Repeated reads are served from memory; every write below invalidates all cached pages, so
            // changes made through this API are never stale. The expiry is a backstop for outside changes.
            var (items, total) = await _cache.GetOrCreateAsync($"inventory:{page}:{pageSize}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                entry.AddExpirationToken(_cacheSignal.Token);
                _logger.LogInformation("Inventory cache miss for page {Page}, loading from database", page);

                var query = _logiTrackContext.InventoryItems.AsNoTracking();
                var pageItems = await query
                    .OrderBy(i => i.ItemId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(i => new InventoryItemDto(i.ItemId, i.Name, i.Quantity, i.Location, i.OrderId))
                    .ToListAsync(cancellationToken);
                return (pageItems, await query.CountAsync(cancellationToken));
            });

            Response.Headers[Paging.TotalCountHeader] = total.ToString();
            // An empty collection is a valid result, not an error
            return items;
        }

        [HttpPost]
        public async Task<ActionResult<InventoryItemDto>> PostInventoryItem([FromBody] CreateInventoryItemDto dto)
        {
            // [ApiController] returns 400 ProblemDetails automatically when the DTO's validation fails
            var inventoryItem = new InventoryItem(dto.Name, dto.Quantity, dto.Location);
            _logiTrackContext.Add(inventoryItem);
            await _logiTrackContext.SaveChangesAsync();
            _cacheSignal.Invalidate();
            return InventoryItemDto.From(inventoryItem);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = Roles.Admin)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<InventoryItemDto>> DeleteInventoryItem(int id, CancellationToken cancellationToken)
        {
            InventoryItem? inventoryItem = await _logiTrackContext.InventoryItems.FindAsync([id], cancellationToken);
            if (inventoryItem == null)
            {
                return InventoryItemNotFound(id);
            }
            _logiTrackContext.Remove(inventoryItem);
            await _logiTrackContext.SaveChangesAsync();
            _cacheSignal.Invalidate();
            return InventoryItemDto.From(inventoryItem);
        }

        private ObjectResult InventoryItemNotFound(int id) =>
            Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Inventory item not found",
                detail: $"No inventory item exists with ID {id}.");
    }
