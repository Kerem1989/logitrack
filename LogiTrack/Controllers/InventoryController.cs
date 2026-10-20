using LogiTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogiTrack.Controllers ;

    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly LogiTrackContext _logiTrackContext;

        public InventoryController(LogiTrackContext logiTrackContext)
        {
            _logiTrackContext = logiTrackContext;
        }

        [HttpGet]
        public ActionResult<IEnumerable<InventoryItem>> GetAllInventoryItems()
        {
            // An empty collection is a valid result, not an error
            return _logiTrackContext.InventoryItems.ToList();
        }

        [HttpPost]
        public ActionResult<InventoryItem> PostInventoryItem([FromBody] InventoryItem inventoryItem)
        {
            if (inventoryItem.Name == null || inventoryItem.Quantity == 0 || inventoryItem.Location == null)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid inventory item",
                    detail: "Name, Location and a non-zero Quantity are required.");
            }
            _logiTrackContext.Add(inventoryItem);
            _logiTrackContext.SaveChanges();
            return inventoryItem;
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public ActionResult<InventoryItem> DeleteInventoryItem(int id)
        {
            InventoryItem? inventoryItem = _logiTrackContext.InventoryItems.Find(id);
            if (inventoryItem == null)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Inventory item not found",
                    detail: $"No inventory item exists with ID {id}.");
            }
            _logiTrackContext.Remove(inventoryItem);
            _logiTrackContext.SaveChanges();
            return inventoryItem;
        }
    }
