using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolSys.Models;

/// <summary>بند رسوم (رسوم دراسية / كتب / زي / نقل ...)</summary>
public class FeeItem
{
    public int Id { get; set; }

    [Required, StringLength(150), Display(Name = "اسم البند")]
    public string Name { get; set; } = string.Empty;

    [StringLength(400), Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Display(Name = "المبلغ الافتراضي"), Column(TypeName = "decimal(18,2)")]
    public decimal DefaultAmount { get; set; }

    [Display(Name = "العام الدراسي")]
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    /// <summary>عند تحديد صف يُطبَّق البند على طلابه فقط.</summary>
    [Display(Name = "الصف")]
    public int? GradeId { get; set; }
    public Grade? Grade { get; set; }

    [Display(Name = "إجباري")]
    public bool IsMandatory { get; set; } = true;

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;
}

/// <summary>فاتورة رسوم الطالب لعام دراسي</summary>
public class Invoice
{
    public int Id { get; set; }

    [Required, StringLength(30), Display(Name = "رقم الفاتورة")]
    public string InvoiceNo { get; set; } = string.Empty;

    [Display(Name = "الطالب")]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Display(Name = "العام الدراسي")]
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    [Display(Name = "تاريخ الإصدار"), DataType(DataType.Date)]
    public DateTime IssueDate { get; set; } = DateTime.Today;

    [Display(Name = "الإجمالي"), Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Display(Name = "الخصم"), Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Display(Name = "الصافي"), Column(TypeName = "decimal(18,2)")]
    public decimal NetAmount { get; set; }

    [Display(Name = "المدفوع"), Column(TypeName = "decimal(18,2)")]
    public decimal PaidAmount { get; set; }

    [Display(Name = "الحالة")]
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;

    [StringLength(500), Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [NotMapped, Display(Name = "المتبقي")]
    public decimal RemainingAmount => NetAmount - PaidAmount;

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public ICollection<Installment> Installments { get; set; } = new List<Installment>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Discount> Discounts { get; set; } = new List<Discount>();
}

/// <summary>سطر بند داخل الفاتورة</summary>
public class InvoiceLine
{
    public int Id { get; set; }

    [Display(Name = "الفاتورة")]
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    [Display(Name = "بند الرسوم")]
    public int? FeeItemId { get; set; }
    public FeeItem? FeeItem { get; set; }

    [Required, StringLength(200), Display(Name = "البيان")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "المبلغ"), Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
}

/// <summary>قسط من أقساط الفاتورة</summary>
public class Installment
{
    public int Id { get; set; }

    [Display(Name = "الفاتورة")]
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    [Display(Name = "رقم القسط")]
    public int SeqNo { get; set; }

    [Required, StringLength(100), Display(Name = "اسم القسط")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "تاريخ الاستحقاق"), DataType(DataType.Date)]
    public DateTime DueDate { get; set; }

    [Display(Name = "المبلغ"), Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Display(Name = "المدفوع"), Column(TypeName = "decimal(18,2)")]
    public decimal PaidAmount { get; set; }

    [Display(Name = "الحالة")]
    public InstallmentStatus Status { get; set; } = InstallmentStatus.Pending;

    [Display(Name = "تم تنبيه ولي الأمر")]
    public bool ReminderSent { get; set; }

    [NotMapped, Display(Name = "المتبقي")]
    public decimal RemainingAmount => Amount - PaidAmount;

    [NotMapped, Display(Name = "متأخر")]
    public bool IsOverdue => Status != InstallmentStatus.Paid && DueDate.Date < DateTime.Today;
}

/// <summary>سند قبض</summary>
public class Payment
{
    public int Id { get; set; }

    [Required, StringLength(30), Display(Name = "رقم الإيصال")]
    public string ReceiptNo { get; set; } = string.Empty;

    [Display(Name = "الفاتورة")]
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    [Display(Name = "القسط")]
    public int? InstallmentId { get; set; }
    public Installment? Installment { get; set; }

    [Display(Name = "الطالب")]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Display(Name = "المبلغ"), Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Display(Name = "تاريخ الدفع"), DataType(DataType.Date)]
    public DateTime PaymentDate { get; set; } = DateTime.Today;

    [Display(Name = "طريقة الدفع")]
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [StringLength(100), Display(Name = "المرجع / رقم الشيك")]
    public string? Reference { get; set; }

    [StringLength(500), Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    [Display(Name = "ملغى")]
    public bool IsCancelled { get; set; }

    [StringLength(300), Display(Name = "سبب الإلغاء")]
    public string? CancelReason { get; set; }

    public int? ReceivedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>خصم على فاتورة طالب</summary>
public class Discount
{
    public int Id { get; set; }

    [Display(Name = "الفاتورة")]
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    [Required, StringLength(150), Display(Name = "اسم الخصم")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "نوع الخصم")]
    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;

    [Display(Name = "القيمة"), Column(TypeName = "decimal(18,2)")]
    public decimal Value { get; set; }

    [Display(Name = "المبلغ المحتسب"), Column(TypeName = "decimal(18,2)")]
    public decimal ComputedAmount { get; set; }

    [StringLength(400), Display(Name = "السبب")]
    public string? Reason { get; set; }

    public int? ApprovedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
