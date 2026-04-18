using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Services;

namespace NewsApp2.Controllers
{
    [Authorize(Policy = "InventoryEditPolicy")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    public class ItemBarcodesController : Controller
    {
        private readonly AppDbContext _context;

        public ItemBarcodesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("Item/{id:guid}/barcodes")]
        public async Task<IActionResult> List(Guid id)
        {
            var item = await _context.Set<Item>()
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
                return NotFound();

            var rawType = BarcodeCodeType.Raw.ToString();
            var list = await _context.Set<BarcodeMapping>()
                .AsNoTracking()
                .Where(m => m.ItemId == id && m.CodeType == rawType)
                .OrderByDescending(m => m.Note == "Primary item barcode")
                .ThenBy(m => m.Code)
                .Select(m => new
                {
                    m.Id,
                    m.Code,
                    IsPrimary = m.Note == "Primary item barcode" || m.Code == item.Barcode,
                    m.Note
                })
                .ToListAsync();

            return Json(list);
        }

        [HttpPost("Item/{id:guid}/barcodes")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Guid id, [FromBody] AddBarcodeRequest request)
        {
            var code = request.Code?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new { success = false, error = "الباركود مطلوب." });
            }

            var item = await _context.Set<Item>().FirstOrDefaultAsync(i => i.Id == id);
            if (item == null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return Json(new { success = false, error = "الصنف غير موجود." });
            }

            if (string.Equals(item.Barcode, code, StringComparison.OrdinalIgnoreCase))
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new { success = false, error = "هذا الباركود هو الأساسي بالفعل." });
            }

            var rawType = BarcodeCodeType.Raw.ToString();

            var mappedToAnother = await _context.Set<BarcodeMapping>()
                .AnyAsync(m => m.Code == code && m.CodeType == rawType && m.ItemId != id);
            if (mappedToAnother)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new { success = false, error = "الباركود يتبع صنفا آخر بالفعل." });
            }

            var existsOnSameItem = await _context.Set<BarcodeMapping>()
                .AnyAsync(m => m.Code == code && m.CodeType == rawType && m.ItemId == id);
            if (existsOnSameItem)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new { success = false, error = "الباركود موجود مسبقا لهذا الصنف." });
            }

            var mapping = new BarcodeMapping
            {
                ItemId = id,
                Code = code,
                CodeType = rawType,
                Note = "Additional barcode"
            };

            _context.Set<BarcodeMapping>().Add(mapping);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpDelete("ItemBarcodes/{mappingId:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid mappingId)
        {
            var mapping = await _context.Set<BarcodeMapping>().FirstOrDefaultAsync(m => m.Id == mappingId);
            if (mapping == null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return Json(new { success = false, error = "الباركود غير موجود." });
            }

            if (string.Equals(mapping.Note, "Primary item barcode", StringComparison.OrdinalIgnoreCase))
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new { success = false, error = "لا يمكن حذف الباركود الأساسي من هذا القسم." });
            }

            _context.Set<BarcodeMapping>().Remove(mapping);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        public sealed class AddBarcodeRequest
        {
            public string? Code { get; set; }
        }
    }
}
