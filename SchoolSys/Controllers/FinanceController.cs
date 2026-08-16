using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Helpers;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[HasPermission(Permissions.FinanceView)]
public class FinanceController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IFinanceService _finance;
    private readonly INotificationService _notify;
    private readonly ICurrentUserService _user;
    private readonly IAuditService _audit;
    private readonly IExportService _export;

    public FinanceController(ApplicationDbContext db, IFinanceService finance, INotificationService notify,
        ICurrentUserService user, IAuditService audit, IExportService export)
    {
        _db = db;
        _finance = finance;
        _notify = notify;
        _user = user;
        _audit = audit;
        _export = export;
    }

    // ==================================================================
    // نظرة عامة
    // ==================================================================
    public async Task<IActionResult> Index()
    {
        var settings = await GetSettingsAsync();
        var year = await GetCurrentYearAsync();
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var vm = new FinanceOverviewViewModel
        {
            Currency = settings.Currency,
            YearName = year?.Name
        };

        var invoices = _db.Invoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Cancelled && (year == null || i.AcademicYearId == year.Id));

        vm.TotalInvoiced = await invoices.SumAsync(i => (decimal?)i.NetAmount) ?? 0m;
        vm.TotalCollected = await invoices.SumAsync(i => (decimal?)i.PaidAmount) ?? 0m;
        vm.TotalDiscount = await invoices.SumAsync(i => (decimal?)i.DiscountAmount) ?? 0m;
        vm.InvoiceCount = await invoices.CountAsync();
        vm.PaidInvoices = await invoices.CountAsync(i => i.Status == InvoiceStatus.Paid);
        vm.UnpaidInvoices = await invoices.CountAsync(i => i.Status == InvoiceStatus.Unpaid);

        vm.CollectedThisMonth = await _db.Payments.AsNoTracking()
            .Where(p => !p.IsCancelled && p.PaymentDate >= monthStart && p.PaymentDate <= today)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var overdue = await _db.Installments.AsNoTracking()
            .Where(i => i.DueDate < today && i.PaidAmount < i.Amount && i.Invoice.Status != InvoiceStatus.Cancelled)
            .Select(i => new { i.Invoice.StudentId, Remaining = i.Amount - i.PaidAmount })
            .ToListAsync();

        vm.OverdueAmount = overdue.Sum(o => o.Remaining);
        vm.OverdueStudents = overdue.Select(o => o.StudentId).Distinct().Count();

        // الإيرادات آخر 12 شهراً
        var since = monthStart.AddMonths(-11);
        var revenue = await _db.Payments.AsNoTracking()
            .Where(p => !p.IsCancelled && p.PaymentDate >= since)
            .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(x => x.Amount) })
            .ToListAsync();

        vm.MonthlyRevenue = Enumerable.Range(0, 12)
            .Select(i => since.AddMonths(i))
            .Select(d => new ChartPoint(d.ToString("MM/yy"),
                revenue.FirstOrDefault(r => r.Year == d.Year && r.Month == d.Month)?.Total ?? 0m))
            .ToList();

        vm.PaymentMethods = (await _db.Payments.AsNoTracking()
                .Where(p => !p.IsCancelled)
                .GroupBy(p => p.Method)
                .Select(g => new { Method = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync())
            .Select(x => new ChartPoint(x.Method.Display(), x.Total))
            .ToList();

        vm.RevenueByGrade = await invoices
            .Where(i => i.Student.CurrentSection != null)
            .GroupBy(i => i.Student.CurrentSection!.Grade.Name)
            .Select(g => new ChartPoint(g.Key, g.Sum(x => x.PaidAmount)))
            .ToListAsync();

        vm.UpcomingInstallments = await _db.Installments.AsNoTracking()
            .Where(i => i.PaidAmount < i.Amount && i.Invoice.Status != InvoiceStatus.Cancelled &&
                        i.DueDate >= today && i.DueDate <= today.AddDays(30))
            .OrderBy(i => i.DueDate)
            .Take(10)
            .Select(i => new InstallmentDueRow
            {
                InstallmentId = i.Id,
                InvoiceId = i.InvoiceId,
                StudentId = i.Invoice.StudentId,
                StudentName = i.Invoice.Student.FullName,
                StudentNo = i.Invoice.Student.StudentNo,
                Section = i.Invoice.Student.CurrentSection != null
                    ? i.Invoice.Student.CurrentSection.Grade.Name + " - " + i.Invoice.Student.CurrentSection.Name : null,
                Name = i.Name,
                DueDate = i.DueDate,
                Amount = i.Amount,
                Paid = i.PaidAmount,
                Status = i.Status
            })
            .ToListAsync();

        vm.RecentPayments = await _db.Payments.AsNoTracking()
            .Where(p => !p.IsCancelled)
            .OrderByDescending(p => p.Id)
            .Take(10)
            .Select(p => new RecentPaymentRow
            {
                Id = p.Id,
                ReceiptNo = p.ReceiptNo,
                StudentName = p.Student.FullName,
                Amount = p.Amount,
                PaymentDate = p.PaymentDate,
                Method = p.Method
            })
            .ToListAsync();

        return View(vm);
    }

    // ==================================================================
    // بنود الرسوم
    // ==================================================================
    [HasPermission(Permissions.FinanceFeeItems)]
    public async Task<IActionResult> FeeItems()
    {
        var year = await GetCurrentYearAsync();
        var settings = await GetSettingsAsync();

        var vm = new FeeItemsViewModel
        {
            CurrentYearId = year?.Id,
            Currency = settings.Currency
        };

        vm.Items = await _db.FeeItems.AsNoTracking()
            .Include(f => f.Grade)
            .Include(f => f.AcademicYear)
            .Where(f => year == null || f.AcademicYearId == year.Id)
            .OrderByDescending(f => f.IsMandatory).ThenBy(f => f.Name)
            .ToListAsync();

        vm.TotalMandatory = vm.Items.Where(i => i.IsMandatory && i.IsActive).Sum(i => i.DefaultAmount);

        vm.Grades = await _db.Grades.AsNoTracking().OrderBy(g => g.SeqNo)
            .Select(g => new SelectListItem(g.Stage.Name + " / " + g.Name, g.Id.ToString())).ToListAsync();

        vm.Years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate)
            .Select(y => new SelectListItem(y.Name, y.Id.ToString())).ToListAsync();

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.FinanceFeeItems)]
    public async Task<IActionResult> SaveFeeItem(FeeItem model)
    {
        if (string.IsNullOrWhiteSpace(model.Name) || model.DefaultAmount < 0)
        {
            Error("الرجاء إدخال اسم البند ومبلغ صحيح.");
            return RedirectToAction(nameof(FeeItems));
        }

        if (model.AcademicYearId == 0)
        {
            var year = await GetCurrentYearAsync();
            if (year is null)
            {
                Error("لا يوجد عام دراسي محدد.");
                return RedirectToAction(nameof(FeeItems));
            }
            model.AcademicYearId = year.Id;
        }

        if (model.GradeId == 0) model.GradeId = null;

        if (model.Id == 0) _db.FeeItems.Add(model);
        else
        {
            var f = await _db.FeeItems.FindAsync(model.Id);
            if (f is null) return NotFound();
            f.Name = model.Name;
            f.Description = model.Description;
            f.DefaultAmount = model.DefaultAmount;
            f.GradeId = model.GradeId;
            f.IsMandatory = model.IsMandatory;
            f.IsActive = model.IsActive;
        }

        await _db.SaveChangesAsync();
        Success("تم حفظ بند الرسوم.");
        return RedirectToAction(nameof(FeeItems));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.FinanceFeeItems)]
    public async Task<IActionResult> DeleteFeeItem(int id)
    {
        if (await _db.InvoiceLines.AnyAsync(l => l.FeeItemId == id))
        {
            Error("لا يمكن حذف البند لاستخدامه في فواتير صادرة. يمكنك تعطيله بدلاً من ذلك.");
            return RedirectToAction(nameof(FeeItems));
        }

        var f = await _db.FeeItems.FindAsync(id);
        if (f is not null)
        {
            _db.FeeItems.Remove(f);
            await _db.SaveChangesAsync();
            Success("تم حذف البند.");
        }
        return RedirectToAction(nameof(FeeItems));
    }

    // ==================================================================
    // الفواتير
    // ==================================================================
    public async Task<IActionResult> Invoices(string? q, InvoiceStatus? status, int? sectionId, int page = 1)
    {
        var settings = await GetSettingsAsync();
        var year = await GetCurrentYearAsync();
        var today = DateTime.Today;

        var vm = new InvoiceIndexViewModel
        {
            Q = q,
            Status = status,
            SectionId = sectionId,
            Page = page,
            Currency = settings.Currency
        };

        var query = _db.Invoices.AsNoTracking()
            .Where(i => year == null || i.AcademicYearId == year.Id);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(i => i.InvoiceNo.Contains(term)
                                     || i.Student.FullName.Contains(term)
                                     || i.Student.StudentNo.Contains(term));
        }

        if (status.HasValue) query = query.Where(i => i.Status == status);
        if (sectionId.HasValue) query = query.Where(i => i.Student.CurrentSectionId == sectionId);

        var projected = query.Select(i => new InvoiceListRow
        {
            Id = i.Id,
            InvoiceNo = i.InvoiceNo,
            StudentId = i.StudentId,
            StudentName = i.Student.FullName,
            StudentNo = i.Student.StudentNo,
            Section = i.Student.CurrentSection != null
                ? i.Student.CurrentSection.Grade.Name + " - " + i.Student.CurrentSection.Name : null,
            IssueDate = i.IssueDate,
            TotalAmount = i.TotalAmount,
            DiscountAmount = i.DiscountAmount,
            NetAmount = i.NetAmount,
            PaidAmount = i.PaidAmount,
            Status = i.Status,
            HasOverdue = i.Installments.Any(x => x.DueDate < today && x.PaidAmount < x.Amount)
        });

        vm.Invoices = await PagedList<InvoiceListRow>.CreateAsync(
            projected.OrderByDescending(i => i.Id), page, 25);

        vm.SumNet = await query.SumAsync(i => (decimal?)i.NetAmount) ?? 0m;
        vm.SumPaid = await query.SumAsync(i => (decimal?)i.PaidAmount) ?? 0m;

        vm.StudentsWithoutInvoice = await _db.Students
            .CountAsync(s => s.Status == StudentStatus.Active &&
                             !_db.Invoices.Any(i => i.StudentId == s.Id &&
                                                    (year == null || i.AcademicYearId == year.Id)));

        vm.Sections = await _db.Sections.AsNoTracking()
            .Where(s => s.IsActive && (year == null || s.AcademicYearId == year.Id))
            .OrderBy(s => s.Grade.SeqNo).ThenBy(s => s.Name)
            .Select(s => new SelectListItem(s.Grade.Name + " - " + s.Name, s.Id.ToString(), s.Id == sectionId))
            .ToListAsync();

        return View(vm);
    }

    public async Task<IActionResult> InvoiceDetails(int id)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Lines).ThenInclude(l => l.FeeItem)
            .Include(i => i.Installments)
            .Include(i => i.Discounts)
            .Include(i => i.Student).ThenInclude(s => s.CurrentSection).ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice is null) return NotFound();

        var settings = await GetSettingsAsync();
        var year = await GetCurrentYearAsync();

        var guardian = await _db.StudentGuardians
            .Where(sg => sg.StudentId == invoice.StudentId)
            .OrderByDescending(sg => sg.IsPrimary)
            .Select(sg => new { sg.Guardian.FullName, sg.Guardian.Phone })
            .FirstOrDefaultAsync();

        var vm = new InvoiceDetailsViewModel
        {
            Invoice = invoice,
            StudentName = invoice.Student.FullName,
            StudentNo = invoice.Student.StudentNo,
            Section = invoice.Student.CurrentSection is null ? null
                : $"{invoice.Student.CurrentSection.Grade.Name} - {invoice.Student.CurrentSection.Name}",
            GuardianName = guardian?.FullName,
            GuardianPhone = guardian?.Phone,
            Currency = settings.Currency,
            Settings = settings
        };

        vm.Payments = await _db.Payments
            .Where(p => p.InvoiceId == id)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id)
            .ToListAsync();

        vm.FeeItems = await _db.FeeItems.AsNoTracking()
            .Where(f => f.IsActive && (year == null || f.AcademicYearId == year.Id))
            .OrderBy(f => f.Name)
            .Select(f => new SelectListItem($"{f.Name} ({f.DefaultAmount:N2})", f.Id.ToString()))
            .ToListAsync();

        return View(vm);
    }

    [HasPermission(Permissions.FinanceInvoices)]
    public async Task<IActionResult> GenerateInvoices()
    {
        var year = await GetCurrentYearAsync();
        if (year is null)
        {
            Error("لا يوجد عام دراسي محدد.");
            return RedirectToAction(nameof(Invoices));
        }

        ViewBag.Sections = await _db.Sections.AsNoTracking()
            .Where(s => s.IsActive && s.AcademicYearId == year.Id)
            .OrderBy(s => s.Grade.SeqNo).ThenBy(s => s.Name)
            .Select(s => new SelectListItem(s.Grade.Name + " - " + s.Name, s.Id.ToString()))
            .ToListAsync();

        ViewBag.Missing = await _db.Students
            .CountAsync(s => s.Status == StudentStatus.Active &&
                             !_db.Invoices.Any(i => i.StudentId == s.Id && i.AcademicYearId == year.Id));

        ViewBag.YearName = year.Name;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.FinanceInvoices)]
    public async Task<IActionResult> GenerateInvoices(int? sectionId, int installments = 4)
    {
        var year = await GetCurrentYearAsync();
        if (year is null)
        {
            Error("لا يوجد عام دراسي محدد.");
            return RedirectToAction(nameof(Invoices));
        }

        var query = _db.Students.Where(s => s.Status == StudentStatus.Active &&
                                            !_db.Invoices.Any(i => i.StudentId == s.Id && i.AcademicYearId == year.Id));

        if (sectionId.HasValue) query = query.Where(s => s.CurrentSectionId == sectionId);

        var studentIds = await query.Select(s => s.Id).ToListAsync();
        if (studentIds.Count == 0)
        {
            Info("جميع الطلاب المحددين لديهم فواتير بالفعل.");
            return RedirectToAction(nameof(Invoices));
        }

        var userId = _user.UserId;
        var created = 0;

        foreach (var studentId in studentIds)
        {
            await _finance.CreateInvoiceForStudentAsync(studentId, year.Id, installments, userId);
            created++;
        }

        await _audit.LogAsync("إصدار فواتير", nameof(Invoice), null, $"{created} فاتورة");
        Success($"تم إصدار {created} فاتورة بنجاح.");
        return RedirectToAction(nameof(Invoices));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.FinanceInvoices)]
    public async Task<IActionResult> AddInvoiceLine(int invoiceId, int? feeItemId, string description, decimal amount)
    {
        var invoice = await _db.Invoices.FindAsync(invoiceId);
        if (invoice is null) return NotFound();

        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            Error("الفاتورة ملغاة.");
            return RedirectToAction(nameof(InvoiceDetails), new { id = invoiceId });
        }

        if (feeItemId.HasValue && string.IsNullOrWhiteSpace(description))
        {
            var fee = await _db.FeeItems.FindAsync(feeItemId.Value);
            description = fee?.Name ?? "بند رسوم";
            if (amount <= 0) amount = fee?.DefaultAmount ?? 0;
        }

        if (string.IsNullOrWhiteSpace(description) || amount <= 0)
        {
            Error("الرجاء إدخال بيان ومبلغ صحيح.");
            return RedirectToAction(nameof(InvoiceDetails), new { id = invoiceId });
        }

        _db.InvoiceLines.Add(new InvoiceLine
        {
            InvoiceId = invoiceId,
            FeeItemId = feeItemId == 0 ? null : feeItemId,
            Description = description,
            Amount = amount
        });

        await _db.SaveChangesAsync();
        await _finance.RecalculateInvoiceAsync(invoiceId);

        Success("تمت إضافة البند وإعادة احتساب الفاتورة.");
        return RedirectToAction(nameof(InvoiceDetails), new { id = invoiceId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.FinanceInvoices)]
    public async Task<IActionResult> DeleteInvoiceLine(int id)
    {
        var line = await _db.InvoiceLines.FindAsync(id);
        if (line is null) return NotFound();

        var invoiceId = line.InvoiceId;
        _db.InvoiceLines.Remove(line);
        await _db.SaveChangesAsync();
        await _finance.RecalculateInvoiceAsync(invoiceId);

        Success("تم حذف البند.");
        return RedirectToAction(nameof(InvoiceDetails), new { id = invoiceId });
    }

    // ==================================================================
    // الخصومات
    // ==================================================================
    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.FinanceDiscounts)]
    public async Task<IActionResult> AddDiscount(int invoiceId, string name, DiscountType discountType,
        decimal value, string? reason)
    {
        var invoice = await _db.Invoices.FindAsync(invoiceId);
        if (invoice is null) return NotFound();

        if (value <= 0 || (discountType == DiscountType.Percentage && value > 100))
        {
            Error("قيمة الخصم غير صحيحة.");
            return RedirectToAction(nameof(InvoiceDetails), new { id = invoiceId });
        }

        _db.Discounts.Add(new Discount
        {
            InvoiceId = invoiceId,
            Name = string.IsNullOrWhiteSpace(name) ? "خصم" : name,
            DiscountType = discountType,
            Value = value,
            Reason = reason,
            ApprovedByUserId = _user.UserId
        });

        await _db.SaveChangesAsync();
        await _finance.RecalculateInvoiceAsync(invoiceId);
        await _audit.LogAsync("إضافة خصم", nameof(Invoice), invoiceId, $"{name} — {value}");

        Success("تم تطبيق الخصم على الفاتورة.");
        return RedirectToAction(nameof(InvoiceDetails), new { id = invoiceId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.FinanceDiscounts)]
    public async Task<IActionResult> DeleteDiscount(int id)
    {
        var d = await _db.Discounts.FindAsync(id);
        if (d is null) return NotFound();

        var invoiceId = d.InvoiceId;
        _db.Discounts.Remove(d);
        await _db.SaveChangesAsync();
        await _finance.RecalculateInvoiceAsync(invoiceId);

        Success("تم إلغاء الخصم.");
        return RedirectToAction(nameof(InvoiceDetails), new { id = invoiceId });
    }

    // ==================================================================
    // المدفوعات
    // ==================================================================
    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.FinancePayments)]
    public async Task<IActionResult> AddPayment(AddPaymentViewModel vm)
    {
        var invoice = await _db.Invoices.FindAsync(vm.InvoiceId);
        if (invoice is null) return NotFound();

        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            Error("لا يمكن تسجيل دفعة على فاتورة ملغاة.");
            return RedirectToAction(nameof(InvoiceDetails), new { id = vm.InvoiceId });
        }

        if (vm.Amount <= 0)
        {
            Error("الرجاء إدخال مبلغ صحيح.");
            return RedirectToAction(nameof(InvoiceDetails), new { id = vm.InvoiceId });
        }

        var remaining = invoice.NetAmount - invoice.PaidAmount;
        if (vm.Amount > remaining)
        {
            Error($"المبلغ المُدخل ({vm.Amount:N2}) يتجاوز المتبقي على الفاتورة ({remaining:N2}).");
            return RedirectToAction(nameof(InvoiceDetails), new { id = vm.InvoiceId });
        }

        var payment = await _finance.RegisterPaymentAsync(vm.InvoiceId, vm.Amount, vm.Method,
            vm.PaymentDate, vm.Reference, vm.Notes, _user.UserId,
            vm.InstallmentId == 0 ? null : vm.InstallmentId);

        await _audit.LogAsync("تسجيل دفعة", nameof(Payment), payment.Id, $"{vm.Amount:N2}");

        Success($"تم تسجيل الدفعة بنجاح — إيصال رقم {payment.ReceiptNo}.");
        return RedirectToAction(nameof(Receipt), new { id = payment.Id });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.FinanceCancelPayment)]
    public async Task<IActionResult> CancelPayment(int id, string reason)
    {
        var payment = await _db.Payments.FindAsync(id);
        if (payment is null) return NotFound();

        if (string.IsNullOrWhiteSpace(reason))
        {
            Error("الرجاء إدخال سبب الإلغاء.");
            return RedirectToAction(nameof(InvoiceDetails), new { id = payment.InvoiceId });
        }

        await _finance.CancelPaymentAsync(id, reason, _user.UserId);
        await _audit.LogAsync("إلغاء سند قبض", nameof(Payment), id, reason);

        Success("تم إلغاء سند القبض وإعادة احتساب الفاتورة.");
        return RedirectToAction(nameof(InvoiceDetails), new { id = payment.InvoiceId });
    }

    public async Task<IActionResult> Payments(string? q, DateTime? from, DateTime? to,
        PaymentMethod? method, bool includeCancelled = false, int page = 1)
    {
        var settings = await GetSettingsAsync();
        var vm = new PaymentIndexViewModel
        {
            Q = q,
            From = from,
            To = to,
            Method = method,
            IncludeCancelled = includeCancelled,
            Page = page,
            Currency = settings.Currency
        };

        var query = _db.Payments.AsNoTracking().AsQueryable();

        if (!includeCancelled) query = query.Where(p => !p.IsCancelled);
        if (from.HasValue) query = query.Where(p => p.PaymentDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(p => p.PaymentDate <= to.Value.Date);
        if (method.HasValue) query = query.Where(p => p.Method == method);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p => p.ReceiptNo.Contains(term)
                                     || p.Student.FullName.Contains(term)
                                     || p.Student.StudentNo.Contains(term)
                                     || p.Invoice.InvoiceNo.Contains(term));
        }

        var projected = query.Select(p => new PaymentListRow
        {
            Id = p.Id,
            ReceiptNo = p.ReceiptNo,
            InvoiceNo = p.Invoice.InvoiceNo,
            InvoiceId = p.InvoiceId,
            StudentId = p.StudentId,
            StudentName = p.Student.FullName,
            StudentNo = p.Student.StudentNo,
            Amount = p.Amount,
            PaymentDate = p.PaymentDate,
            Method = p.Method,
            Reference = p.Reference,
            IsCancelled = p.IsCancelled
        });

        vm.Payments = await PagedList<PaymentListRow>.CreateAsync(
            projected.OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id), page, 25);

        vm.TotalAmount = await query.Where(p => !p.IsCancelled).SumAsync(p => (decimal?)p.Amount) ?? 0m;
        vm.TotalCount = await query.CountAsync();

        return View(vm);
    }

    public async Task<IActionResult> Receipt(int id)
    {
        var payment = await _db.Payments
            .Include(p => p.Invoice)
            .Include(p => p.Student).ThenInclude(s => s.CurrentSection).ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment is null) return NotFound();

        var settings = await GetSettingsAsync();
        var guardian = await _db.StudentGuardians
            .Where(sg => sg.StudentId == payment.StudentId)
            .OrderByDescending(sg => sg.IsPrimary)
            .Select(sg => sg.Guardian.FullName)
            .FirstOrDefaultAsync();

        return View(new ReceiptViewModel
        {
            Payment = payment,
            Invoice = payment.Invoice,
            Student = payment.Student,
            Section = payment.Student.CurrentSection is null ? null
                : $"{payment.Student.CurrentSection.Grade.Name} - {payment.Student.CurrentSection.Name}",
            GuardianName = guardian,
            Settings = settings,
            RemainingAfter = payment.Invoice.NetAmount - payment.Invoice.PaidAmount,
            AmountInWords = NumberToArabicWords.Convert(payment.Amount, settings.Currency)
        });
    }

    // ==================================================================
    // المتأخرات
    // ==================================================================
    public async Task<IActionResult> Overdue(int? sectionId, int minDaysOverdue = 0)
    {
        var settings = await GetSettingsAsync();
        var year = await GetCurrentYearAsync();
        var today = DateTime.Today;

        var vm = new OverdueViewModel
        {
            SectionId = sectionId,
            MinDaysOverdue = minDaysOverdue,
            Currency = settings.Currency
        };

        vm.Sections = await _db.Sections.AsNoTracking()
            .Where(s => s.IsActive && (year == null || s.AcademicYearId == year.Id))
            .OrderBy(s => s.Grade.SeqNo).ThenBy(s => s.Name)
            .Select(s => new SelectListItem(s.Grade.Name + " - " + s.Name, s.Id.ToString(), s.Id == sectionId))
            .ToListAsync();

        var cutoff = today.AddDays(-minDaysOverdue);

        var query = _db.Installments.AsNoTracking()
            .Where(i => i.DueDate <= cutoff && i.PaidAmount < i.Amount &&
                        i.Invoice.Status != InvoiceStatus.Cancelled);

        if (sectionId.HasValue)
            query = query.Where(i => i.Invoice.Student.CurrentSectionId == sectionId);

        vm.Rows = await query
            .OrderBy(i => i.DueDate)
            .Take(1000)
            .Select(i => new InstallmentDueRow
            {
                InstallmentId = i.Id,
                InvoiceId = i.InvoiceId,
                StudentId = i.Invoice.StudentId,
                StudentName = i.Invoice.Student.FullName,
                StudentNo = i.Invoice.Student.StudentNo,
                Section = i.Invoice.Student.CurrentSection != null
                    ? i.Invoice.Student.CurrentSection.Grade.Name + " - " + i.Invoice.Student.CurrentSection.Name : null,
                Name = i.Name,
                DueDate = i.DueDate,
                Amount = i.Amount,
                Paid = i.PaidAmount,
                Status = i.Status,
                ReminderSent = i.ReminderSent,
                GuardianPhone = i.Invoice.Student.StudentGuardians
                    .OrderByDescending(sg => sg.IsPrimary)
                    .Select(sg => sg.Guardian.Phone).FirstOrDefault()
            })
            .ToListAsync();

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.NotificationsSend)]
    public async Task<IActionResult> SendReminders(List<int> installmentIds, int? sectionId)
    {
        if (installmentIds is null || installmentIds.Count == 0)
        {
            Warning("لم يتم اختيار أي قسط.");
            return RedirectToAction(nameof(Overdue), new { sectionId });
        }

        var settings = await GetSettingsAsync();

        var items = await _db.Installments
            .Where(i => installmentIds.Contains(i.Id))
            .Select(i => new
            {
                i.Id,
                i.Name,
                i.DueDate,
                Remaining = i.Amount - i.PaidAmount,
                i.Invoice.StudentId,
                StudentName = i.Invoice.Student.FullName
            })
            .ToListAsync();

        foreach (var item in items)
        {
            await _notify.NotifyGuardiansOfStudentAsync(item.StudentId,
                "تذكير بسداد الرسوم",
                $"نذكّركم بأن {item.Name} الخاص بالطالب {item.StudentName} " +
                $"بمبلغ {item.Remaining:N2} {settings.Currency} مستحق منذ {item.DueDate:yyyy/MM/dd}. " +
                "يرجى المراجعة مع قسم المحاسبة.",
                NotificationType.Fees, NotificationSeverity.Warning);
        }

        var toMark = await _db.Installments.Where(i => installmentIds.Contains(i.Id)).ToListAsync();
        foreach (var i in toMark) i.ReminderSent = true;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("إرسال تنبيهات أقساط", nameof(Installment), null, $"{items.Count} تنبيه");
        Success($"تم إرسال {items.Count} تنبيه لأولياء الأمور.");
        return RedirectToAction(nameof(Overdue), new { sectionId });
    }

    // ==================================================================
    // التقارير المالية
    // ==================================================================
    [HasPermission(Permissions.FinanceReports)]
    public async Task<IActionResult> Reports(FinanceReportViewModel filter)
    {
        var settings = await GetSettingsAsync();
        var vm = filter;
        vm.Currency = settings.Currency;

        if (vm.From == default) vm.From = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (vm.To == default) vm.To = DateTime.Today;

        var query = _db.Payments.AsNoTracking()
            .Where(p => !p.IsCancelled && p.PaymentDate >= vm.From.Date && p.PaymentDate <= vm.To.Date);

        vm.Rows = vm.GroupBy switch
        {
            "method" => (await query.GroupBy(p => p.Method)
                    .Select(g => new { Key = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount) })
                    .ToListAsync())
                .Select(x => new FinanceReportRow { Label = x.Key.Display(), Count = x.Count, Amount = x.Amount })
                .OrderByDescending(r => r.Amount).ToList(),

            "grade" => await query
                .Where(p => p.Student.CurrentSection != null)
                .GroupBy(p => p.Student.CurrentSection!.Grade.Name)
                .Select(g => new FinanceReportRow { Label = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount) })
                .OrderByDescending(r => r.Amount).ToListAsync(),

            "day" => await query
                .GroupBy(p => p.PaymentDate)
                .Select(g => new FinanceReportRow
                {
                    Label = g.Key.ToString("yyyy/MM/dd"),
                    Count = g.Count(),
                    Amount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(r => r.Label).ToListAsync(),

            _ => (await query
                    .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count(), Amount = g.Sum(x => x.Amount) })
                    .ToListAsync())
                .Select(x => new FinanceReportRow
                {
                    Label = $"{x.Month:D2}/{x.Year}",
                    Count = x.Count,
                    Amount = x.Amount
                })
                .OrderByDescending(r => r.Label).ToList()
        };

        return View(vm);
    }

    [HasPermission(Permissions.ReportsExport)]
    public async Task<IActionResult> ExportInvoices(string? q, InvoiceStatus? status, int? sectionId, string format = "excel")
    {
        var year = await GetCurrentYearAsync();
        var settings = await GetSettingsAsync();

        var query = _db.Invoices.AsNoTracking().Where(i => year == null || i.AcademicYearId == year.Id);
        if (status.HasValue) query = query.Where(i => i.Status == status);
        if (sectionId.HasValue) query = query.Where(i => i.Student.CurrentSectionId == sectionId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(i => i.InvoiceNo.Contains(term) || i.Student.FullName.Contains(term));
        }

        var rows = await query.OrderBy(i => i.InvoiceNo).Take(5000)
            .Select(i => new
            {
                i.InvoiceNo,
                i.Student.StudentNo,
                Student = i.Student.FullName,
                Section = i.Student.CurrentSection != null
                    ? i.Student.CurrentSection.Grade.Name + " - " + i.Student.CurrentSection.Name : "",
                i.TotalAmount,
                i.DiscountAmount,
                i.NetAmount,
                i.PaidAmount,
                i.Status
            })
            .ToListAsync();

        var columns = new List<ExportColumn>
        {
            new("رقم الفاتورة", 1.2f), new("الرقم الطلابي", 1f), new("الطالب", 2.2f), new("الشعبة", 1.2f),
            new("الإجمالي", 1f), new("الخصم", .9f), new("الصافي", 1f), new("المدفوع", 1f),
            new("المتبقي", 1f), new("الحالة", 1f)
        };

        var data = rows.Select(r => new string?[]
        {
            r.InvoiceNo, r.StudentNo, r.Student, r.Section,
            r.TotalAmount.ToString("N2"), r.DiscountAmount.ToString("N2"),
            r.NetAmount.ToString("N2"), r.PaidAmount.ToString("N2"),
            (r.NetAmount - r.PaidAmount).ToString("N2"), r.Status.Display()
        });

        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            return File(_export.ToPdf("كشف الفواتير", $"عدد الفواتير: {rows.Count}", columns, data, settings.SchoolName),
                "application/pdf", $"invoices-{DateTime.Now:yyyyMMdd}.pdf");

        return File(_export.ToExcel("كشف الفواتير", columns, data),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"invoices-{DateTime.Now:yyyyMMdd}.xlsx");
    }
}
