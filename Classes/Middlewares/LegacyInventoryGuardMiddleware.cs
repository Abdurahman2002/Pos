namespace NewsApp2.Classes.Middlewares
{
    public class LegacyInventoryGuardMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;

        public LegacyInventoryGuardMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var disableLegacyTransfers = _configuration.GetValue<bool?>("InventoryModernization:DisableLegacyTransfers") ?? true;
            var disableLegacyMedicalFlows = _configuration.GetValue<bool?>("InventoryModernization:DisableLegacyMedicalFlows") ?? true;
            var disableWarehouseManagement = _configuration.GetValue<bool?>("InventoryModernization:DisableWarehouseManagement") ?? true;

            if (disableLegacyTransfers && context.Request.Path.StartsWithSegments("/Transfers", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status410Gone;
                await context.Response.WriteAsync("Legacy transfer flow is disabled under single-warehouse mode.");
                return;
            }

            if (disableLegacyMedicalFlows &&
                (context.Request.Path.StartsWithSegments("/Receipts", StringComparison.OrdinalIgnoreCase)
                 || context.Request.Path.StartsWithSegments("/Issues", StringComparison.OrdinalIgnoreCase)
                 || context.Request.Path.StartsWithSegments("/StockBalances/Expiry", StringComparison.OrdinalIgnoreCase)
                 || context.Request.Path.StartsWithSegments("/StockBalances/Opening", StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = StatusCodes.Status410Gone;
                await context.Response.WriteAsync("Legacy medical inventory flow is disabled. Use Purchase and Sales modules.");
                return;
            }

            if (disableWarehouseManagement && context.Request.Path.StartsWithSegments("/Warehouses", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status410Gone;
                await context.Response.WriteAsync("Warehouse management is disabled in single-warehouse mode.");
                return;
            }

            await _next(context);
        }
    }
}
