using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[HasPermission(Permissions.GuardiansView)]
public class GuardiansController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditService _audit;
    private readonly IExportService _export;

    public GuardiansController(ApplicationDbContext db, UserManager<ApplicationUser> users,
        IAuditService audit, IExportService export)
    {
        _db = db;
        _users = users;
        _audit = audit;
        _export = export;
    }

    public async Task<IActionResult> Index(string? q, bool? hasAccount, int page = 1)
    {
        var vm = new GuardianIndexViewModel { Q = q, HasAccount = hasAccount, Page = page };

        var query = _db.Guardians.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(g => g.FullName.Contains(term)
                                     || g.Phone.Contains(term)
                                     || (g.NationalId != null && g.NationalId.Contains(term))
                                     || (g.Email != null && g.Email.Contains(term)));
        }

        var projected = query.Select(g => new GuardianListRow
        {
            Id = g.Id,
            FullName = g.FullName,
            Phone = g.Phone,
            Email = g.Email,
            Job = g.Job,
            IsActive = g.IsActive,
            ChildrenCount = g.StudentGuardians.Count,
            Children = string.Join("، ", g.StudentGuardians.Select(sg => sg.Student.FullName)),
            HasAccount = _db.Users.Any(u => u.GuardianId == g.Id),
            Outstanding = g.StudentGuardians
                .SelectMany(sg => sg.Student.Invoices)
                .Where(i => i.Status != InvoiceStatus.Cancelled)
                .Sum(i => (decimal?)(i.NetAmount - i.PaidAmount)) ?? 0m
        });

        if (hasAccount.HasValue)
            projected = projected.Where(g => g.HasAccount == hasAccount.Value);

        vm.Guardians = await PagedList<GuardianListRow>.CreateAsync(
            projected.OrderBy(g => g.FullName), page, 25);

        vm.TotalGuardians = await _db.Guardians.CountAsync();
        vm.WithAccounts = await _db.Users.CountAsync(u => u.GuardianId != null);

        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var guardian = await _db.Guardians.FirstOrDefaultAsync(g => g.Id == id);
        if (guardian is null) return NotFound();

        var settings = await GetSettingsAsync();
        var account = await _db.Users.FirstOrDefaultAsync(u => u.GuardianId == id);

        var children = await _db.StudentGuardians
            .Where(sg => sg.GuardianId == id)
            .Select(sg => new GuardianChildRow
            {
                LinkId = sg.Id,
                StudentId = sg.StudentId,
                StudentNo = sg.Student.StudentNo,
                FullName = sg.Student.FullName,
                PhotoPath = sg.Student.PhotoPath,
                Section = sg.Student.CurrentSection != null
                    ? sg.Student.CurrentSection.Grade.Name + " - " + sg.Student.CurrentSection.Name
                    : null,
                Relation = sg.Relation,
                IsPrimary = sg.IsPrimary,
                Status = sg.Student.Status,
                Outstanding = sg.Student.Invoices.Where(i => i.Status != InvoiceStatus.Cancelled)
                    .Sum(i => (decimal?)(i.NetAmount - i.PaidAmount)) ?? 0m
            })
            .ToListAsync();

        // نسبة حضور كل ابن
        var studentIds = children.Select(c => c.StudentId).ToList();
        var attendance = await _db.StudentAttendances
            .Where(a => studentIds.Contains(a.StudentId))
            .GroupBy(a => a.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Total = g.Count(),
                Present = g.Count(x => x.Status == AttendanceStatus.Present || x.Status == AttendanceStatus.Late)
            })
            .ToListAsync();

        foreach (var c in children)
        {
            var a = attendance.FirstOrDefault(x => x.StudentId == c.StudentId);
            c.AttendanceRate = a is { Total: > 0 } ? Math.Round((double)a.Present / a.Total * 100, 1) : 0;
        }

        return View(new GuardianDetailsViewModel
        {
            Guardian = guardian,
            Children = children,
            HasAccount = account is not null,
            AccountEmail = account?.Email,
            Currency = settings.Currency
        });
    }

    [HasPermission(Permissions.GuardiansCreate)]
    public IActionResult Create() => View("Form", new GuardianFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.GuardiansCreate)]
    public async Task<IActionResult> Create(GuardianFormViewModel vm)
    {
        if (!ModelState.IsValid) return View("Form", vm);

        var guardian = new Guardian
        {
            FullName = vm.FullName.Trim(),
            NationalId = vm.NationalId,
            Phone = vm.Phone.Trim(),
            AltPhone = vm.AltPhone,
            Email = vm.Email,
            Job = vm.Job,
            Workplace = vm.Workplace,
            Address = vm.Address,
            IsActive = vm.IsActive
        };

        _db.Guardians.Add(guardian);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("إضافة ولي أمر", nameof(Guardian), guardian.Id, guardian.FullName);

        Success("تمت إضافة ولي الأمر بنجاح.");
        return RedirectToAction(nameof(Details), new { id = guardian.Id });
    }

    [HasPermission(Permissions.GuardiansEdit)]
    public async Task<IActionResult> Edit(int id)
    {
        var g = await _db.Guardians.FindAsync(id);
        if (g is null) return NotFound();

        return View("Form", new GuardianFormViewModel
        {
            Id = g.Id,
            FullName = g.FullName,
            NationalId = g.NationalId,
            Phone = g.Phone,
            AltPhone = g.AltPhone,
            Email = g.Email,
            Job = g.Job,
            Workplace = g.Workplace,
            Address = g.Address,
            IsActive = g.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.GuardiansEdit)]
    public async Task<IActionResult> Edit(GuardianFormViewModel vm)
    {
        if (!ModelState.IsValid) return View("Form", vm);

        var g = await _db.Guardians.FindAsync(vm.Id);
        if (g is null) return NotFound();

        g.FullName = vm.FullName.Trim();
        g.NationalId = vm.NationalId;
        g.Phone = vm.Phone.Trim();
        g.AltPhone = vm.AltPhone;
        g.Email = vm.Email;
        g.Job = vm.Job;
        g.Workplace = vm.Workplace;
        g.Address = vm.Address;
        g.IsActive = vm.IsActive;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("تعديل ولي أمر", nameof(Guardian), g.Id, g.FullName);

        Success("تم حفظ البيانات.");
        return RedirectToAction(nameof(Details), new { id = g.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.GuardiansDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var g = await _db.Guardians.FindAsync(id);
        if (g is null) return NotFound();

        var account = await _db.Users.FirstOrDefaultAsync(u => u.GuardianId == id);
        if (account is not null)
        {
            Error("لا يمكن حذف ولي الأمر لوجود حساب مرتبط به. قم بحذف الحساب أولاً.");
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.Guardians.Remove(g);   // روابط الأبناء تُحذف تلقائياً
        await _db.SaveChangesAsync();
        await _audit.LogAsync("حذف ولي أمر", nameof(Guardian), id, g.FullName);

        Success("تم حذف ولي الأمر.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>بحث سريع يُستخدم في نافذة ربط ولي الأمر بالطالب.</summary>
    [HttpGet]
    public async Task<IActionResult> Search(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Json(Array.Empty<object>());

        var term = q.Trim();
        var items = await _db.Guardians.AsNoTracking()
            .Where(g => g.FullName.Contains(term) || g.Phone.Contains(term))
            .OrderBy(g => g.FullName)
            .Take(12)
            .Select(g => new { id = g.Id, name = g.FullName, phone = g.Phone })
            .ToListAsync();

        return Json(items);
    }

    // ---------------- إنشاء حساب دخول ----------------
    [HasPermission(Permissions.UsersManage)]
    public async Task<IActionResult> CreateAccount(int id)
    {
        var g = await _db.Guardians.FindAsync(id);
        if (g is null) return NotFound();

        if (await _db.Users.AnyAsync(u => u.GuardianId == id))
        {
            Warning("يوجد حساب مرتبط بولي الأمر بالفعل.");
            return RedirectToAction(nameof(Details), new { id });
        }

        return View("~/Views/Shared/_CreateAccount.cshtml", new CreateAccountViewModel
        {
            EntityId = g.Id,
            EntityName = g.FullName,
            EntityType = "Guardian",
            Email = g.Email ?? $"g{g.Phone}@school.local",
            Role = RoleNames.Guardian
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.UsersManage)]
    public async Task<IActionResult> CreateAccount(CreateAccountViewModel vm)
    {
        if (!ModelState.IsValid)
            return View("~/Views/Shared/_CreateAccount.cshtml", vm);

        var g = await _db.Guardians.FindAsync(vm.EntityId);
        if (g is null) return NotFound();

        var user = new ApplicationUser
        {
            UserName = vm.Email,
            Email = vm.Email,
            EmailConfirmed = true,
            FullName = g.FullName,
            PhoneNumber = g.Phone,
            GuardianId = g.Id,
            IsActive = true
        };

        var result = await _users.CreateAsync(user, vm.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, AccountController.TranslateIdentityError(e));
            return View("~/Views/Shared/_CreateAccount.cshtml", vm);
        }

        await _users.AddToRoleAsync(user, RoleNames.Guardian);
        await _audit.LogAsync("إنشاء حساب ولي أمر", nameof(Guardian), g.Id, vm.Email);

        Success($"تم إنشاء حساب ولي الأمر «{vm.Email}» بنجاح.");
        return RedirectToAction(nameof(Details), new { id = g.Id });
    }

    [HasPermission(Permissions.ReportsExport)]
    public async Task<IActionResult> Export(string? q, string format = "excel")
    {
        var query = _db.Guardians.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(g => g.FullName.Contains(term) || g.Phone.Contains(term));
        }

        var rows = await query.OrderBy(g => g.FullName).Take(5000)
            .Select(g => new
            {
                g.FullName,
                g.Phone,
                g.AltPhone,
                g.Email,
                g.Job,
                Children = string.Join("، ", g.StudentGuardians.Select(sg => sg.Student.FullName))
            })
            .ToListAsync();

        var settings = await GetSettingsAsync();
        var columns = new List<ExportColumn>
        {
            new("اسم ولي الأمر", 2f), new("الجوال", 1f), new("هاتف بديل", 1f),
            new("البريد الإلكتروني", 1.6f), new("المهنة", 1.2f), new("الأبناء", 2.6f)
        };

        var data = rows.Select(r => new string?[] { r.FullName, r.Phone, r.AltPhone, r.Email, r.Job, r.Children });

        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            return File(_export.ToPdf("كشف أولياء الأمور", $"عدد السجلات: {rows.Count}", columns, data, settings.SchoolName),
                "application/pdf", $"guardians-{DateTime.Now:yyyyMMdd}.pdf");

        return File(_export.ToExcel("كشف أولياء الأمور", columns, data),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"guardians-{DateTime.Now:yyyyMMdd}.xlsx");
    }
}
