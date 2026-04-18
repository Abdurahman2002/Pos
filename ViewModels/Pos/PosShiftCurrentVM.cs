using System.ComponentModel.DataAnnotations;
using NewsApp2.Models.Entities;

namespace NewsApp2.ViewModels.Pos
{
    public class PosShiftCurrentVM
    {
        public PosShift? ActiveShift { get; set; }

        [Range(typeof(decimal), "0", "999999999999")]
        public decimal OpeningCashLyd { get; set; }

        [Range(typeof(decimal), "0", "999999999999")]
        public decimal ClosingCashLyd { get; set; }

        public int SalesCount { get; set; }

        public decimal SalesTotalLyd { get; set; }

        public decimal CashSalesLyd { get; set; }

        public decimal CardSalesLyd { get; set; }

        public decimal TransferSalesLyd { get; set; }

        public decimal CreditSalesLyd { get; set; }

        public decimal ExpectedCashLyd { get; set; }

        public decimal CashDifferenceLyd { get; set; }
    }
}
