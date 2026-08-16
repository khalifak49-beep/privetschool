using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[HasPermission(Permissions.ReportsView)]
public class ReportsController : BaseController
{
    private readonly ApplicationDbContext _db;

    public ReportsController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var settings = await GetSettingsAsync();
        var year = await GetCurrentYearAsync();

        return View(new ReportsCenterViewModel
        {
            Students = await _db.Students.CountAsync(s => s.Status == StudentStatus.Active),
            Employees = await _db.Employees.CountAsync(e => e.IsActive),
            Sections = await _db.Sections.CountAsync(s => s.IsActive && (year == null || s.AcademicYearId == year.Id)),
            Invoices = await _db.Invoices.CountAsync(i => year == null || i.AcademicYearId == year.Id),
            AttendanceRecords = await _db.StudentAttendances.CountAsync(),
            ExamResults = await _db.ExamResults.CountAsync(),
            Currency = settings.Currency,
            YearName = year?.Name
        });
    }
}
