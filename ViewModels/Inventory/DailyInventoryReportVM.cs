using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Inventory
{
    public class DailyInventoryRowVM
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;

        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal OpeningBalance { get; set; }

        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal Received { get; set; }

        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal Sold { get; set; }

        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal Returns { get; set; }

        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal ClosingBalance { get; set; }
    }

    public class DailyInventoryReportVM
    {
        [DataType(DataType.Date)]
        public DateOnly ReportDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        public List<DailyInventoryRowVM> Rows { get; set; } = new();

        public decimal TotalOpening => Rows.Sum(x => x.OpeningBalance);
        public decimal TotalReceived => Rows.Sum(x => x.Received);
        public decimal TotalSold => Rows.Sum(x => x.Sold);
        public decimal TotalReturns => Rows.Sum(x => x.Returns);
        public decimal TotalClosing => Rows.Sum(x => x.ClosingBalance);
    }
}
