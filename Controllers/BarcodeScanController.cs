using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Services;
using NewsApp2.ViewModels.Inventory;

namespace NewsApp2.Controllers
{
    [Authorize(Policy = "ApprovedUserPolicy")]
    public class BarcodeScanController : Controller
    {
        private readonly AppDbContext _context;
        private readonly BarcodeScanService _scanService;

        public BarcodeScanController(AppDbContext context, BarcodeScanService scanService)
        {
            _context = context;
            _scanService = scanService;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve([FromBody] BarcodeResolveRequestVM request)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, error = "الطلب غير صالح." });
            }

            var result = await _scanService.ResolveAsync(request.Code, request.WarehouseId);

            if (!result.Success && result.RequiresMapping)
            {
                return Json(new
                {
                    success = false,
                    requiresMapping = true,
                    code = result.Code,
                    codeType = result.CodeType.ToString(),
                    gtin = result.Gtin,
                    batchNo = result.BatchNo,
                    expiryDate = FormatDate(result.ExpiryDate)
                });
            }

            if (!result.Success)
            {
                return Json(new { success = false, error = result.Error ?? "تعذر التعرف على الباركود." });
            }

            return Json(new
            {
                success = true,
                itemId = result.ItemId,
                itemName = result.ItemName,
                batchNo = result.BatchNo,
                expiryDate = FormatDate(result.ExpiryDate),
                batchAvailable = result.BatchAvailable
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Map([FromBody] BarcodeMapRequestVM request)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, error = "الطلب غير صالح." });
            }

            var codeType = request.CodeType?.Trim();
            if (!Enum.TryParse<BarcodeCodeType>(codeType, out var parsedType))
            {
                return Json(new { success = false, error = "نوع الكود غير صالح." });
            }

            var itemExists = await _context.Set<Item>()
                .AsNoTracking()
                .AnyAsync(i => i.Id == request.ItemId);

            if (!itemExists)
            {
                return Json(new { success = false, error = "الصنف غير موجود." });
            }

            var normalized = request.Code.Trim();
            var existing = await _context.Set<BarcodeMapping>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Code == normalized && m.CodeType == parsedType.ToString());

            if (existing != null)
            {
                if (existing.ItemId != request.ItemId)
                {
                    return Json(new { success = false, error = "الباركود مرتبط بصنف آخر بالفعل." });
                }

                return Json(new { success = true });
            }

            // Also check primary barcodes stored directly on Item
            var usedAsPrimary = await _context.Set<Item>()
                .IgnoreQueryFilters()
                .AnyAsync(i => i.Barcode == normalized && i.Id != request.ItemId);
            if (usedAsPrimary)
            {
                return Json(new { success = false, error = "الباركود مستخدم كباركود أساسي لصنف آخر." });
            }

            var mapping = new BarcodeMapping
            {
                Code = normalized,
                CodeType = parsedType.ToString(),
                ItemId = request.ItemId
            };

            _context.Set<BarcodeMapping>().Add(mapping);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        private static string? FormatDate(DateOnly? date)
        {
            return date.HasValue ? date.Value.ToString("dd/MM/yyyy") : null;
        }
    }
}
