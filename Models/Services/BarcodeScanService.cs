using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewsApp2.Models.Entities;

namespace NewsApp2.Models.Services
{
    public sealed class BarcodeScanService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BarcodeScanService> _logger;

        public BarcodeScanService(AppDbContext context, ILogger<BarcodeScanService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<BarcodeResolveResult> ResolveAsync(string rawCode, Guid? warehouseId)
        {
            var normalized = NormalizeCode(rawCode);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return BarcodeResolveResult.CreateFailure("Barcode is empty.");
            }
            try
            {
                // Try Raw type first (most common — internal + manually entered barcodes)
                var rawMapping = await _context.Set<BarcodeMapping>()
                    .AsNoTracking()
                    .Include(m => m.Item)
                    .FirstOrDefaultAsync(m => m.Code == normalized && m.CodeType == BarcodeCodeType.Raw.ToString());

                if (rawMapping != null)
                {
                    return BarcodeResolveResult.CreateSuccess(rawMapping, new BarcodeParseResult { Raw = normalized });
                }

                // Fallback: try GTIN type (GS1/retail barcodes mapped via scan-and-map flow)
                var gtinMapping = await _context.Set<BarcodeMapping>()
                    .AsNoTracking()
                    .Include(m => m.Item)
                    .FirstOrDefaultAsync(m => m.Code == normalized && m.CodeType == BarcodeCodeType.Gtin.ToString());

                if (gtinMapping != null)
                {
                    return BarcodeResolveResult.CreateSuccess(gtinMapping, new BarcodeParseResult { Raw = normalized, Gtin = normalized });
                }

                // Not found in any mapping — prompt user to map it
                return BarcodeResolveResult.CreateRequiresMapping(normalized, BarcodeCodeType.Raw, new BarcodeParseResult { Raw = normalized });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Barcode scan DB error for code {Code}", normalized);
                return BarcodeResolveResult.CreateFailure("تعذر الاتصال بقاعدة البيانات. حاول مرة أخرى.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Barcode scan error for code {Code}", normalized);
                return BarcodeResolveResult.CreateFailure("حدث خطأ غير متوقع أثناء قراءة الباركود.");
            }
        }

        private static string NormalizeCode(string raw)
        {
            return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim();
        }

    }

    public enum BarcodeCodeType
    {
        Raw,
        Gtin
    }

    public sealed class BarcodeParseResult
    {
        public string Raw { get; init; } = string.Empty;
        public bool IsGs1 { get; set; }
        public string? Gtin { get; set; }
        public string? BatchNo { get; set; }
        public DateOnly? ExpiryDate { get; set; }

        public bool HasBatchDetails => !string.IsNullOrWhiteSpace(BatchNo) && ExpiryDate.HasValue;
    }

    public sealed class BarcodeResolveResult
    {
        public bool Success { get; init; }
        public string? Error { get; init; }
        public Guid? ItemId { get; init; }
        public string? ItemName { get; init; }
        public string Code { get; init; } = string.Empty;
        public BarcodeCodeType CodeType { get; init; }
        public bool RequiresMapping { get; init; }
        public bool? BatchAvailable { get; init; }
        public string? BatchNo { get; init; }
        public DateOnly? ExpiryDate { get; init; }
        public string? Gtin { get; init; }

        public static BarcodeResolveResult CreateFailure(string message)
        {
            return new BarcodeResolveResult { Success = false, Error = message };
        }

        public static BarcodeResolveResult CreateRequiresMapping(string rawCode, BarcodeCodeType codeType, BarcodeParseResult parse)
        {
            return new BarcodeResolveResult
            {
                Success = false,
                RequiresMapping = true,
                Code = codeType == BarcodeCodeType.Gtin ? (parse.Gtin ?? rawCode) : rawCode,
                CodeType = codeType,
                Gtin = parse.Gtin,
                BatchNo = parse.BatchNo,
                ExpiryDate = parse.ExpiryDate
            };
        }

        public static BarcodeResolveResult CreateSuccess(BarcodeMapping mapping, BarcodeParseResult parse)
        {
            return new BarcodeResolveResult
            {
                Success = true,
                ItemId = mapping.ItemId,
                ItemName = mapping.Item?.Name,
                Code = mapping.Code,
                CodeType = Enum.TryParse<BarcodeCodeType>(mapping.CodeType, out var type) ? type : BarcodeCodeType.Raw,
                RequiresMapping = false,
                Gtin = parse.Gtin,
                BatchNo = parse.BatchNo,
                ExpiryDate = parse.ExpiryDate
            };
        }

        public BarcodeResolveResult WithBatchAvailability(bool available)
        {
            return new BarcodeResolveResult
            {
                Success = Success,
                Error = Error,
                ItemId = ItemId,
                ItemName = ItemName,
                Code = Code,
                CodeType = CodeType,
                RequiresMapping = RequiresMapping,
                BatchAvailable = available,
                BatchNo = BatchNo,
                ExpiryDate = ExpiryDate,
                Gtin = Gtin
            };
        }
    }
}
