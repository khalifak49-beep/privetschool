using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolSys.Models;
using System.ComponentModel.DataAnnotations;

namespace SchoolSys.ViewModels;

public class FinanceOverviewViewModel
{
    public string Currency { get; set; } = "ر.ع";
    public string? YearName { get; set; }

    public decimal TotalInvoiced { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalOutstanding => TotalInvoiced - TotalCollected;
    public decimal OverdueAmount { get; set; }
    public int OverdueStudents { get; set; }
    public decimal CollectedThisMonth { get; set; }
    public int InvoiceCount { get; set; }
    public int PaidInvoices { get; set; }
    public int UnpaidInvoices { get; set; }

    public double CollectionRate => TotalInvoiced > 0 ? (double)(TotalCollected / TotalInvoiced * 100m) : 0;

    public List<ChartPoint> MonthlyRevenue { get; set; } = [];
    public List<ChartPoint> PaymentMethods { get; set; } = [];
    public List<ChartPoint> RevenueByGrade { get; set; } = [];
    public List<InstallmentDueRow> UpcomingInstallments { get; set; } = [];
    public List<RecentPaymentRow> RecentPayments { get; set; } = [];
}

public class InstallmentDueRow
{
    public int InstallmentId { get; set; }
    public int InvoiceId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = "";
    public string StudentNo { get; set; } = "";
    public string? Section { get; set; }
    public string Name { get; set; } = "";
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal Paid { get; set; }
    public decimal Remaining => Amount - Paid;
    public InstallmentStatus Status { get; set; }
    public string? GuardianPhone { get; set; }
    public bool ReminderSent { get; set; }
    public int DaysOverdue => (DateTime.Today - DueDate.Date).Days;
}

public class InvoiceIndexViewModel
{
    public string? Q { get; set; }
    public InvoiceStatus? Status { get; set; }
    public int? SectionId { get; set; }
    public int Page { get; set; } = 1;
    public string Currency { get; set; } = "ر.ع";

    public PagedList<InvoiceListRow> Invoices { get; set; } = PagedList<InvoiceListRow>.Empty();
    public List<SelectListItem> Sections { get; set; } = [];

    public decimal SumNet { get; set; }
    public decimal SumPaid { get; set; }
    public int StudentsWithoutInvoice { get; set; }
}

public class InvoiceListRow
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = "";
    public int StudentId { get; set; }
    public string StudentName { get; set; } = "";
    public string StudentNo { get; set; } = "";
    public string? Section { get; set; }
    public DateTime IssueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Remaining => NetAmount - PaidAmount;
    public InvoiceStatus Status { get; set; }
    public bool HasOverdue { get; set; }
    public double PaidRate => NetAmount > 0 ? (double)(PaidAmount / NetAmount * 100m) : 0;
}

public class InvoiceDetailsViewModel
{
    public Invoice Invoice { get; set; } = null!;
    public string StudentName { get; set; } = "";
    public string StudentNo { get; set; } = "";
    public string? Section { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string Currency { get; set; } = "ر.ع";
    public List<Payment> Payments { get; set; } = [];
    public List<SelectListItem> FeeItems { get; set; } = [];
    public SchoolSetting Settings { get; set; } = null!;
}

public class FeeItemsViewModel
{
    public List<FeeItem> Items { get; set; } = [];
    public List<SelectListItem> Grades { get; set; } = [];
    public List<SelectListItem> Years { get; set; } = [];
    public int? CurrentYearId { get; set; }
    public string Currency { get; set; } = "ر.ع";
    public decimal TotalMandatory { get; set; }
}

public class PaymentIndexViewModel
{
    public string? Q { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public PaymentMethod? Method { get; set; }
    public bool IncludeCancelled { get; set; }
    public int Page { get; set; } = 1;
    public string Currency { get; set; } = "ر.ع";

    public PagedList<PaymentListRow> Payments { get; set; } = PagedList<PaymentListRow>.Empty();
    public decimal TotalAmount { get; set; }
    public int TotalCount { get; set; }
}

public class PaymentListRow
{
    public int Id { get; set; }
    public string ReceiptNo { get; set; } = "";
    public string InvoiceNo { get; set; } = "";
    public int InvoiceId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = "";
    public string StudentNo { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public bool IsCancelled { get; set; }
}

public class OverdueViewModel
{
    public int? SectionId { get; set; }
    public int MinDaysOverdue { get; set; }
    public string Currency { get; set; } = "ر.ع";
    public List<SelectListItem> Sections { get; set; } = [];
    public List<InstallmentDueRow> Rows { get; set; } = [];
    public decimal TotalOverdue => Rows.Sum(r => r.Remaining);
    public int StudentCount => Rows.Select(r => r.StudentId).Distinct().Count();
}

public class FinanceReportViewModel
{
    public DateTime From { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime To { get; set; } = DateTime.Today;
    public string GroupBy { get; set; } = "month";   // month | method | grade | day
    public string Currency { get; set; } = "ر.ع";
    public List<FinanceReportRow> Rows { get; set; } = [];
    public decimal Total => Rows.Sum(r => r.Amount);
    public int Count => Rows.Sum(r => r.Count);
}

public class FinanceReportRow
{
    public string Label { get; set; } = "";
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public class AddPaymentViewModel
{
    public int InvoiceId { get; set; }

    [Range(0.01, 1000000, ErrorMessage = "الرجاء إدخال مبلغ صحيح")]
    [Display(Name = "المبلغ")]
    public decimal Amount { get; set; }

    [Display(Name = "تاريخ الدفع"), DataType(DataType.Date)]
    public DateTime PaymentDate { get; set; } = DateTime.Today;

    [Display(Name = "طريقة الدفع")]
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [StringLength(100), Display(Name = "المرجع / رقم الشيك")]
    public string? Reference { get; set; }

    [StringLength(500), Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    [Display(Name = "خصم من القسط")]
    public int? InstallmentId { get; set; }
}

public class ReceiptViewModel
{
    public Payment Payment { get; set; } = null!;
    public Invoice Invoice { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public string? Section { get; set; }
    public string? GuardianName { get; set; }
    public SchoolSetting Settings { get; set; } = null!;
    public decimal RemainingAfter { get; set; }
    public string AmountInWords { get; set; } = "";
}
