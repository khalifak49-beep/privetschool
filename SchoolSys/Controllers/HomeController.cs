using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Models;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _db;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    /// <summary>الصفحة التعريفية العامة للمدرسة.</summary>
    public async Task<IActionResult> Index()
    {
        var settings = await _db.SchoolSettings.AsNoTracking().FirstOrDefaultAsync() ?? new SchoolSetting();
        var year = await _db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);

        var vm = new LandingViewModel
        {
            SchoolName = settings.SchoolName,
            SchoolNameEn = settings.SchoolNameEn,
            LogoPath = settings.LogoPath,
            Address = settings.Address,
            Phone = settings.Phone,
            Email = settings.Email,
            Website = settings.Website,
            StartTime = settings.SchoolStartTime,
            EndTime = settings.SchoolEndTime,
            YearName = year?.Name
        };

        vm.Students = await _db.Students.CountAsync(s => s.Status == StudentStatus.Active);
        vm.Teachers = await _db.Employees.CountAsync(e => e.EmployeeType == EmployeeType.Teacher && e.IsActive);
        vm.Subjects = await _db.Subjects.CountAsync(s => s.IsActive);
        vm.Buses = await _db.Buses.CountAsync(b => b.IsActive);
        vm.Sections = year is null
            ? await _db.Sections.CountAsync(s => s.IsActive)
            : await _db.Sections.CountAsync(s => s.IsActive && s.AcademicYearId == year.Id);

        // سنوات الخدمة تُحتسب من أقدم تاريخ تعيين
        var firstHire = await _db.Employees.OrderBy(e => e.HireDate).Select(e => (DateTime?)e.HireDate).FirstOrDefaultAsync();
        vm.YearsOfService = firstHire.HasValue
            ? Math.Max(1, (int)((DateTime.Today - firstHire.Value).TotalDays / 365.2425))
            : 1;

        // المراحل مع أعمار الطلاب الفعلية
        var stages = await _db.Stages.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.SeqNo)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.SeqNo,
                GradesCount = s.Grades.Count(g => g.IsActive),
                FirstGrade = s.Grades.OrderBy(g => g.SeqNo).Select(g => g.Name).FirstOrDefault(),
                LastGrade = s.Grades.OrderByDescending(g => g.SeqNo).Select(g => g.Name).FirstOrDefault(),
                StudentsCount = _db.Students.Count(st => st.Status == StudentStatus.Active &&
                                                         st.CurrentSection!.Grade.StageId == s.Id)
            })
            .ToListAsync();

        // حساب الأعمار من تواريخ ميلاد الطلاب فعلياً
        var birthStats = await _db.Students.AsNoTracking()
            .Where(st => st.Status == StudentStatus.Active && st.BirthDate != null && st.CurrentSectionId != null)
            .Select(st => new { StageId = st.CurrentSection!.Grade.StageId, st.BirthDate })
            .ToListAsync();

        var ageByStage = birthStats
            .GroupBy(x => x.StageId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var ages = g.Select(x => (int)((DateTime.Today - x.BirthDate!.Value).TotalDays / 365.2425)).ToList();
                    return (Min: ages.Min(), Max: ages.Max());
                });

        vm.Stages = stages.Select(s =>
        {
            var card = new StageCard
            {
                Name = s.Name,
                SeqNo = s.SeqNo,
                GradesCount = s.GradesCount,
                StudentsCount = s.StudentsCount,
                GradeRange = s.FirstGrade is null ? "" :
                    s.FirstGrade == s.LastGrade ? s.FirstGrade : $"{s.FirstGrade} — {s.LastGrade}"
            };

            if (ageByStage.TryGetValue(s.Id, out var age))
            {
                card.MinAge = age.Min;
                card.MaxAge = age.Max;
            }

            return card;
        }).ToList();

        return View(vm);
    }

    /// <summary>
    /// اسم المدرسة وشعارها لصفحات الخطأ — فهي ترث القالب العام لكن هذا
    /// المتحكّم لا يرث BaseController الذي يهيّئهما. يبتلع أي عطل لأن
    /// صفحة الخطأ نفسها يجب ألّا تنهار.
    /// </summary>
    private async Task SetBrandingAsync()
    {
        try
        {
            var s = await _db.SchoolSettings.AsNoTracking().FirstOrDefaultAsync();
            if (s is null) return;
            ViewBag.SchoolName = s.SchoolName;
            ViewBag.SchoolNameEn = s.SchoolNameEn;
            ViewBag.LogoPath = s.LogoPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "تعذّرت قراءة إعدادات المدرسة لصفحة الخطأ.");
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Error()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (feature is not null)
            _logger.LogError(feature.Error, "خطأ غير متوقع في المسار {Path}", feature.Path);

        await SetBrandingAsync();
        ViewBag.RequestId = HttpContext.TraceIdentifier;
        return View();
    }

    public async Task<IActionResult> StatusCode(int? code)
    {
        await SetBrandingAsync();
        ViewBag.Code = code ?? 500;
        return View("StatusCodeError");
    }
}
