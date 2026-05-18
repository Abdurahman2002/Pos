using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NewsApp2.Classes.Helpers
{
    public class DecimalModelBinder : IModelBinder
    {
        private static readonly CultureInfo[] Cultures =
        {
            CultureInfo.CurrentCulture,
            new("en-US"),
            new("en-GB"),
            new("ar-IQ"),
            CultureInfo.InvariantCulture
        };

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext == null) throw new ArgumentNullException(nameof(bindingContext));

            var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueResult == ValueProviderResult.None)
            {
                return Task.CompletedTask;
            }

            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);
            var raw = valueResult.FirstValue;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Task.CompletedTask;
            }

            // Try parsing with several cultures to accept both '.' and ',' decimal separators
            foreach (var culture in Cultures)
            {
                if (decimal.TryParse(raw, NumberStyles.Number | NumberStyles.AllowLeadingSign, culture, out var parsed))
                {
                    bindingContext.Result = ModelBindingResult.Success(parsed);
                    return Task.CompletedTask;
                }
            }

            // As a fallback, try to normalize common separators: replace comma with dot and parse invariant
            var normalized = raw.Replace(',', '.');
            if (decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var normalizedParsed))
            {
                bindingContext.Result = ModelBindingResult.Success(normalizedParsed);
                return Task.CompletedTask;
            }

            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "قيمة رقمية غير صالحة.");
            return Task.CompletedTask;
        }
    }

    public class DecimalModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var modelType = context.Metadata.ModelType;
            if (modelType == typeof(decimal) || modelType == typeof(decimal?))
            {
                return new DecimalModelBinder();
            }
            return null;
        }
    }
}
