using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NewsApp2.Classes.Helpers
{
    public class DateOnlyModelBinder : IModelBinder
    {
        private static readonly string[] Formats = { "dd/MM/yyyy", "d/M/yyyy" };
        private static readonly CultureInfo Culture = new("en-GB");

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext == null)
            {
                throw new ArgumentNullException(nameof(bindingContext));
            }

            var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueResult == ValueProviderResult.None)
            {
                return Task.CompletedTask;
            }

            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);
            var rawValue = valueResult.FirstValue;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return Task.CompletedTask;
            }

            if (DateOnly.TryParseExact(rawValue, Formats, Culture, DateTimeStyles.None, out var parsed))
            {
                bindingContext.Result = ModelBindingResult.Success(parsed);
            }
            else
            {
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Invalid date format. Use dd/MM/yyyy.");
            }

            return Task.CompletedTask;
        }
    }

    public class DateOnlyModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var modelType = context.Metadata.ModelType;
            if (modelType == typeof(DateOnly) || modelType == typeof(DateOnly?))
            {
                return new DateOnlyModelBinder();
            }

            return null;
        }
    }
}
