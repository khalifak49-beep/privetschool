using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SchoolSys.Data;
using SchoolSys.Models;

namespace SchoolSys.Controllers;

[Authorize]
public abstract class BaseController : Controller
{
    private const string SettingsCacheKey = "school-settings";

    protected ApplicationDbContext Db => HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();

    protected IMemoryCache Cache => HttpContext.RequestServices.GetRequiredService<IMemoryCache>();

    /// <summary>إعدادات المدرسة (مخزّنة مؤقتاً).</summary>
    protected async Task<SchoolSetting> GetSettingsAsync()
    {
        if (Cache.TryGetValue(SettingsCacheKey, out SchoolSetting? cached) && cached is not null)
            return cached;

        var settings = await Db.SchoolSettings.AsNoTracking().FirstOrDefaultAsync()
                       ?? new SchoolSetting();

        Cache.Set(SettingsCacheKey, settings, TimeSpan.FromMinutes(15));
        return settings;
    }

    protected void ClearSettingsCache() => Cache.Remove(SettingsCacheKey);

    /// <summary>العام الدراسي الحالي.</summary>
    protected async Task<AcademicYear?> GetCurrentYearAsync()
        => await Db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent)
           ?? await Db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).FirstOrDefaultAsync();

    protected async Task<Term?> GetCurrentTermAsync()
        => await Db.Terms.AsNoTracking().FirstOrDefaultAsync(t => t.IsCurrent);

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var settings = await GetSettingsAsync();
        ViewBag.SchoolName = settings.SchoolName;
        ViewBag.SchoolNameEn = settings.SchoolNameEn;
        // الشعار متاح لكل صفحة في النظام: القائمة والترويسة وشاشة الدخول
        // والمستندات المطبوعة وأيقونة المتصفح
        ViewBag.LogoPath = settings.LogoPath;
        ViewBag.Currency = settings.Currency;
        await next();
    }

    protected void Success(string message) => TempData["Success"] = message;
    protected void Error(string message) => TempData["Error"] = message;
    protected void Warning(string message) => TempData["Warning"] = message;
    protected void Info(string message) => TempData["Info"] = message;
}
