using System;

namespace NewsApp2.Models.Exceptions
{
    public class BusinessRuleException : InvalidOperationException
    {
        public string Code { get; }
        public object? Meta { get; }

        public BusinessRuleException(string message, string code = "BUSINESS_RULE", object? meta = null)
            : base(message)
        {
            Code = code;
            Meta = meta;
        }
    }
}
