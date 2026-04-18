using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Expenses
{
    public class ExpenseEntryFormVM
    {
        public Guid? Id { get; set; }

        [Display(Name = "التاريخ")]
        public DateOnly ExpenseDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        [Display(Name = "المبلغ")]
        [Range(typeof(decimal), "0.01", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
        public decimal Amount { get; set; }

        [Display(Name = "نوع الحركة")]
        [Required]
        [StringLength(20)]
        public string ExpenseKind { get; set; } = "General";

        [Display(Name = "التصنيف")]
        [Required]
        [StringLength(100)]
        public string Category { get; set; } = string.Empty;

        [Display(Name = "الموظف")]
        public Guid? EmployeeId { get; set; }

        [Display(Name = "طريقة الدفع")]
        [Required]
        [StringLength(20)]
        public string PaymentMethod { get; set; } = "Cash";

        [Display(Name = "رقم مرجعي")]
        [StringLength(50)]
        public string? ReferenceNo { get; set; }

        [Display(Name = "ملاحظة")]
        [StringLength(500)]
        public string? Note { get; set; }

        [Display(Name = "تطبيق تسوية السلفة")]
        public bool ApplyAdvanceSettlement { get; set; }

        [Display(Name = "قيمة الخصم من السلفة")]
        [Range(typeof(decimal), "0", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
        public decimal AdvanceDeduction { get; set; }
    }

    public class ExpenseEntryRowVM
    {
        public Guid Id { get; set; }
        public DateOnly ExpenseDate { get; set; }
        public string ExpenseKind { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? EmployeeName { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? ReferenceNo { get; set; }
        public string? Note { get; set; }
        public DateTime Created { get; set; }
        public string? CreatedByUserName { get; set; }
    }

    public class ExpenseEntryIndexVM
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        public string? ExpenseKind { get; set; }
        public Guid? EmployeeId { get; set; }
        public string? Search { get; set; }

        public decimal GeneralExpensesTotal { get; set; }
        public decimal SalariesTotal { get; set; }
        public decimal AdvancesTotal { get; set; }
        public decimal SettlementsTotal { get; set; }
        public decimal OverallTotal { get; set; }

        public List<ExpenseEntryRowVM> Rows { get; set; } = new();
    }

    public class EmployeeExpenseSummaryRowVM
    {
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public decimal SalariesTotal { get; set; }
        public decimal AdvancesTotal { get; set; }
        public decimal SettlementsTotal { get; set; }
        public decimal OutstandingAdvanceBalance { get; set; }
        public decimal NetDue { get; set; }
        public DateOnly? LastMovementDate { get; set; }
    }

    public class EmployeeExpenseSummaryVM
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public Guid? EmployeeId { get; set; }

        public decimal TotalSalaries { get; set; }
        public decimal TotalAdvances { get; set; }
        public decimal TotalSettlements { get; set; }
        public decimal TotalOutstandingAdvances { get; set; }
        public decimal TotalNetDue { get; set; }

        public List<EmployeeExpenseSummaryRowVM> Rows { get; set; } = new();
    }
}
