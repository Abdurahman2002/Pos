using Microsoft.EntityFrameworkCore;
using NewsApp2.Models.Entities;

namespace NewsApp2.Models.Services
{
    public sealed class BarcodeScanService
    {
        private readonly AppDbContext _context;

        public BarcodeScanService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<BarcodeResolveResult> ResolveAsync(string rawCode, Guid? warehouseId)
        {
            var normalized = NormalizeCode(rawCode);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return BarcodeResolveResult.CreateFailure("Barcode is empty.");
            }

            var lookupType = BarcodeCodeType.Raw;
            var lookupCode = normalized;

            var mapping = await _context.Set<BarcodeMapping>()
                .AsNoTracking()
                .Include(m => m.Item)
                .FirstOrDefaultAsync(m => m.Code == lookupCode && m.CodeType == lookupType.ToString());

            if (mapping == null)
            {
                return BarcodeResolveResult.CreateRequiresMapping(normalized, lookupType, new BarcodeParseResult { Raw = normalized });
            }

            return BarcodeResolveResult.CreateSuccess(mapping, new BarcodeParseResult { Raw = normalized });
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
