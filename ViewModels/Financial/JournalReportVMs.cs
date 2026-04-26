namespace NewsApp2.ViewModels.Financial
{
    public class JournalEntryRowVM
    {
        public DateOnly EntryDate { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string? DocumentNo { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string? Note { get; set; }
        public string? CreatedByUserName { get; set; }
    }

    public class JournalReportVM
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public string? AccountCode { get; set; }
        public string? SourceType { get; set; }
        public string? Search { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public List<JournalEntryRowVM> Rows { get; set; } = new();
        public List<string> AccountCodes { get; set; } = new();
        public List<string> SourceTypes { get; set; } = new();
    }

    public class TrialBalanceRowVM
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal NetDebit { get; set; }
        public decimal NetCredit { get; set; }
    }

    public class TrialBalanceReportVM
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal TotalNetDebit { get; set; }
        public decimal TotalNetCredit { get; set; }
        public List<TrialBalanceRowVM> Rows { get; set; } = new();
    }
}
