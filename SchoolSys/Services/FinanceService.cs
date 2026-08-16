using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Models;

namespace SchoolSys.Services;

public interface IFinanceService
{
    Task<string> NextInvoiceNoAsync();
    Task<string> NextReceiptNoAsync();

    /// <summary>يعيد احتساب إجماليات الفاتورة وحالتها من سطورها وخصوماتها ومدفوعاتها.</summary>
    Task RecalculateInvoiceAsync(int invoiceId);

    /// <summary>يسجّل دفعة ويوزّعها على الأقساط المستحقة بالترتيب.</summary>
    Task<Payment> RegisterPaymentAsync(int invoiceId, decimal amount, PaymentMethod method,
        DateTime paymentDate, string? reference, string? notes, int? userId, int? installmentId = null);

    Task CancelPaymentAsync(int paymentId, string reason, int? userId);

    /// <summary>ينشئ فاتورة لطالب من بنود الرسوم المطبقة على صفه.</summary>
    Task<Invoice> CreateInvoiceForStudentAsync(int studentId, int academicYearId, int installmentCount, int? userId);
}

public class FinanceService : IFinanceService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;

    public FinanceService(ApplicationDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<string> NextInvoiceNoAsync()
    {
        var year = DateTime.Today.Year;
        var prefix = $"INV-{year}-";
        var last = await _db.Invoices.Where(i => i.InvoiceNo.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNo).Select(i => i.InvoiceNo).FirstOrDefaultAsync();
        return prefix + NextSeq(last, prefix);
    }

    public async Task<string> NextReceiptNoAsync()
    {
        var year = DateTime.Today.Year;
        var prefix = $"RCP-{year}-";
        var last = await _db.Payments.Where(p => p.ReceiptNo.StartsWith(prefix))
            .OrderByDescending(p => p.ReceiptNo).Select(p => p.ReceiptNo).FirstOrDefaultAsync();
        return prefix + NextSeq(last, prefix);
    }

    private static string NextSeq(string? last, string prefix)
    {
        var n = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last[prefix.Length..], out var parsed))
            n = parsed + 1;
        return n.ToString("D5");
    }

    public async Task RecalculateInvoiceAsync(int invoiceId)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Discounts)
            .Include(i => i.Installments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice is null) return;

        invoice.TotalAmount = invoice.Lines.Sum(l => l.Amount);

        foreach (var d in invoice.Discounts)
            d.ComputedAmount = d.DiscountType == DiscountType.Percentage
                ? Math.Round(invoice.TotalAmount * d.Value / 100m, 2)
                : d.Value;

        invoice.DiscountAmount = invoice.Discounts.Sum(d => d.ComputedAmount);
        invoice.NetAmount = Math.Max(0, invoice.TotalAmount - invoice.DiscountAmount);

        invoice.PaidAmount = await _db.Payments
            .Where(p => p.InvoiceId == invoiceId && !p.IsCancelled)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        if (invoice.Status != InvoiceStatus.Cancelled)
            invoice.Status = invoice.PaidAmount <= 0 ? InvoiceStatus.Unpaid
                : invoice.PaidAmount >= invoice.NetAmount ? InvoiceStatus.Paid
                : InvoiceStatus.PartiallyPaid;

        foreach (var inst in invoice.Installments)
        {
            inst.Status = inst.PaidAmount <= 0
                ? (inst.DueDate.Date < DateTime.Today ? InstallmentStatus.Overdue : InstallmentStatus.Pending)
                : inst.PaidAmount >= inst.Amount ? InstallmentStatus.Paid
                : InstallmentStatus.Partial;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<Payment> RegisterPaymentAsync(int invoiceId, decimal amount, PaymentMethod method,
        DateTime paymentDate, string? reference, string? notes, int? userId, int? installmentId = null)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Installments)
            .Include(i => i.Student)
            .FirstAsync(i => i.Id == invoiceId);

        var payment = new Payment
        {
            ReceiptNo = await NextReceiptNoAsync(),
            InvoiceId = invoiceId,
            InstallmentId = installmentId,
            StudentId = invoice.StudentId,
            Amount = amount,
            PaymentDate = paymentDate,
            Method = method,
            Reference = reference,
            Notes = notes,
            ReceivedByUserId = userId
        };
        _db.Payments.Add(payment);

        // توزيع المبلغ على الأقساط: القسط المحدد أولاً ثم الأقدم استحقاقاً
        var remaining = amount;
        var ordered = invoice.Installments
            .OrderBy(i => installmentId.HasValue && i.Id == installmentId.Value ? 0 : 1)
            .ThenBy(i => i.DueDate)
            .ToList();

        foreach (var inst in ordered)
        {
            if (remaining <= 0) break;
            var due = inst.Amount - inst.PaidAmount;
            if (due <= 0) continue;

            var applied = Math.Min(due, remaining);
            inst.PaidAmount += applied;
            remaining -= applied;
        }

        await _db.SaveChangesAsync();
        await RecalculateInvoiceAsync(invoiceId);

        await _notifications.NotifyGuardiansOfStudentAsync(
            invoice.StudentId,
            "تم استلام دفعة",
            $"تم استلام مبلغ {amount:N2} عن الطالب {invoice.Student.FullName}. رقم الإيصال: {payment.ReceiptNo}",
            NotificationType.Fees, NotificationSeverity.Success,
            $"/Portal/Guardian/Fees");

        return payment;
    }

    public async Task CancelPaymentAsync(int paymentId, string reason, int? userId)
    {
        var payment = await _db.Payments
            .Include(p => p.Invoice).ThenInclude(i => i.Installments)
            .FirstOrDefaultAsync(p => p.Id == paymentId);
        if (payment is null || payment.IsCancelled) return;

        payment.IsCancelled = true;
        payment.CancelReason = reason;

        // سحب المبلغ من الأقساط بترتيب عكسي
        var remaining = payment.Amount;
        foreach (var inst in payment.Invoice.Installments.OrderByDescending(i => i.DueDate))
        {
            if (remaining <= 0) break;
            var taken = Math.Min(inst.PaidAmount, remaining);
            inst.PaidAmount -= taken;
            remaining -= taken;
        }

        await _db.SaveChangesAsync();
        await RecalculateInvoiceAsync(payment.InvoiceId);
    }

    public async Task<Invoice> CreateInvoiceForStudentAsync(
        int studentId, int academicYearId, int installmentCount, int? userId)
    {
        var student = await _db.Students
            .Include(s => s.CurrentSection)
            .FirstAsync(s => s.Id == studentId);

        var gradeId = student.CurrentSection?.GradeId;

        var items = await _db.FeeItems
            .Where(f => f.AcademicYearId == academicYearId && f.IsActive &&
                        (f.GradeId == null || f.GradeId == gradeId))
            .ToListAsync();

        var invoice = new Invoice
        {
            InvoiceNo = await NextInvoiceNoAsync(),
            StudentId = studentId,
            AcademicYearId = academicYearId,
            IssueDate = DateTime.Today,
            CreatedByUserId = userId,
            Lines = items.Select(f => new InvoiceLine
            {
                FeeItemId = f.Id,
                Description = f.Name,
                Amount = f.DefaultAmount
            }).ToList()
        };

        var total = invoice.Lines.Sum(l => l.Amount);
        invoice.TotalAmount = total;
        invoice.NetAmount = total;

        var count = Math.Max(1, installmentCount);
        var each = Math.Round(total / count, 2);
        for (var i = 0; i < count; i++)
        {
            invoice.Installments.Add(new Installment
            {
                SeqNo = i + 1,
                Name = $"القسط {i + 1}",
                DueDate = DateTime.Today.AddMonths(i * 2),
                Amount = i == count - 1 ? total - each * (count - 1) : each
            });
        }

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();
        return invoice;
    }
}
