using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[HasPermission(Permissions.SettingsManage)]
public class SettingsController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _files;
    private readonly IAuditService _audit;

    public SettingsController(ApplicationDbContext db, IFileStorageService files, IAuditService audit)
    {
        _db = db;
        _files = files;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var s = await _db.SchoolSettings.FirstOrDefaultAsync();
        if (s is null)
        {
            s = new SchoolSetting();
            _db.SchoolSettings.Add(s);
            await _db.SaveChangesAsync();
        }

        return View(new SettingsViewModel
        {
            Id = s.Id,
            SchoolName = s.SchoolName,
            SchoolNameEn = s.SchoolNameEn,
            Address = s.Address,
            Phone = s.Phone,
            Email = s.Email,
            Website = s.Website,
            Currency = s.Currency,
            SchoolStartTime = s.SchoolStartTime,
            SchoolEndTime = s.SchoolEndTime,
            LateGraceMinutes = s.LateGraceMinutes,
            AutoNotifyGuardianOnAbsence = s.AutoNotifyGuardianOnAbsence,
            EnableSms = s.EnableSms,
            EnableWhatsApp = s.EnableWhatsApp,
            LogoPath = s.LogoPath
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SettingsViewModel vm)
    {
        if (vm.SchoolEndTime <= vm.SchoolStartTime)
            ModelState.AddModelError(nameof(vm.SchoolEndTime), "نهاية الدوام يجب أن تكون بعد بدايته.");

        if (!ModelState.IsValid) return View(vm);

        var s = await _db.SchoolSettings.FirstOrDefaultAsync();
        if (s is null) return NotFound();

        s.SchoolName = vm.SchoolName.Trim();
        s.SchoolNameEn = vm.SchoolNameEn;
        s.Address = vm.Address;
        s.Phone = vm.Phone;
        s.Email = vm.Email;
        s.Website = vm.Website;
        s.Currency = vm.Currency.Trim();
        s.SchoolStartTime = vm.SchoolStartTime;
        s.SchoolEndTime = vm.SchoolEndTime;
        s.LateGraceMinutes = vm.LateGraceMinutes;
        s.AutoNotifyGuardianOnAbsence = vm.AutoNotifyGuardianOnAbsence;
        s.EnableSms = vm.EnableSms;
        s.EnableWhatsApp = vm.EnableWhatsApp;

        if (vm.Logo is not null)
        {
            try
            {
                var path = await _files.SaveAsync(vm.Logo, "school", IFileStorageService.ImageExtensions, 2 * 1024 * 1024);
                if (path is not null)
                {
                    _files.Delete(s.LogoPath);
                    s.LogoPath = path;
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(vm.Logo), ex.Message);
                vm.LogoPath = s.LogoPath;
                return View(vm);
            }
        }

        await _db.SaveChangesAsync();
        ClearSettingsCache();
        await _audit.LogAsync("تعديل إعدادات المدرسة", nameof(SchoolSetting), s.Id);

        Success("تم حفظ الإعدادات بنجاح.");
        return RedirectToAction(nameof(Index));
    }
}

[HasPermission(Permissions.AuditView)]
public class AuditController : BaseController
{
    private readonly ApplicationDbContext _db;

    public AuditController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? q, string? action, DateTime? from, DateTime? to, int page = 1)
    {
        var vm = new AuditIndexViewModel { Q = q, Action = action, From = from, To = to, Page = page };

        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(l => (l.UserName != null && l.UserName.Contains(term)) ||
                                     (l.Details != null && l.Details.Contains(term)) ||
                                     (l.EntityName != null && l.EntityName.Contains(term)));
        }

        if (!string.IsNullOrEmpty(action)) query = query.Where(l => l.Action == action);
        if (from.HasValue) query = query.Where(l => l.CreatedAt >= from.Value.Date);
        if (to.HasValue) query = query.Where(l => l.CreatedAt < to.Value.Date.AddDays(1));

        vm.Logs = await PagedList<AuditLog>.CreateAsync(query.OrderByDescending(l => l.Id), page, 40);

        vm.Actions = await _db.AuditLogs.Select(l => l.Action).Distinct().OrderBy(a => a).Take(60).ToListAsync();

        return View(vm);
    }
}
