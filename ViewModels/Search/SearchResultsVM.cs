using System;
using System.Collections.Generic;

namespace NewsApp2.ViewModels.Search
{
    public class SearchResultsVM
    {
        public string Query { get; set; } = string.Empty;
        public bool CanSearchInvoices { get; set; }

        public List<SearchItemRow> Items { get; set; } = new();
        public List<SearchPartyRow> Customers { get; set; } = new();
        public List<SearchPartyRow> Suppliers { get; set; } = new();
        public List<SearchInvoiceRow> Invoices { get; set; } = new();

        public int TotalResults => Items.Count + Customers.Count + Suppliers.Count + Invoices.Count;
    }

    public class SearchItemRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string? Category { get; set; }
    }

    public class SearchPartyRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
    }

    public class SearchInvoiceRow
    {
        public Guid Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public decimal TotalDinar { get; set; }
        public DateOnly InvoiceDate { get; set; }
    }
}
