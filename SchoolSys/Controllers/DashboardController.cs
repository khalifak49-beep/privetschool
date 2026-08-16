using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[HasPermission(Permissions.DashboardView)]
public class DashboardController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;

    public DashboardController(ApplicationDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task<IActionResult> Index()
    {
        // توجيه الطلاب وأولياء الأمور إلى بواباتهم الخاصة
        if (User.IsInRole(RoleNames.Student))
            return RedirectToAction("Index", "Student", new { area = "Portal" });

        if (User.IsInRole(RoleNames.Guardian))
            return RedirectToAction("Index", "Guardian", new { area = "Portal" });

        if (User.IsInRole(RoleNames.Teacher) && !User.Can(Permissions.StudentsCreate))
            return await TeacherDashboardAsync();

        return await AdminDashboardAsync();
    }

    // ------------------------------------------------------------------
    private async Task<IActionResult> AdminDashboardAsync()
    {
        var settings = await GetSettingsAsync();
        var year = await GetCurrentYearAsync();
        var term = await GetCurrentTermAsync();
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var vm = new DashboardViewModel
        {
            SchoolName = settings.SchoolName,
            Currency = settings.Currency,
            AcademicYearName = year?.Name,
            TermName = term?.Name
        };

        vm.TotalStudents = await _db.Students.CountAsync(s => s.Status == StudentStatus.Active);
        vm.TotalTeachers = await _db.Employees.CountAsync(e => e.EmployeeType == EmployeeType.Teacher && e.IsActive);
        vm.TotalGuardians = await _db.Guardians.CountAsync(g => g.IsActive);
        vm.TotalSections = year is null
            ? await _db.Sections.CountAsync(s => s.IsActive)
            : await _db.Sections.CountAsync(s => s.IsActive && s.AcademicYearId == year.Id);
        vm.NewStudentsThisMonth = await _db.Students.CountAsync(s => s.EnrollmentDate >= monthStart);

        // ---------- الحضور اليوم ----------
        var todayStats = await _db.StudentAttendances
            .Where(a => a.Date == today)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        vm.PresentToday = todayStats.Where(s => s.Status is AttendanceStatus.Present or AttendanceStatus.Late)
            .Sum(s => s.Count);
        vm.AbsentToday = todayStats.Where(s => s.Status == AttendanceStatus.Absent).Sum(s => s.Count);
        vm.LateToday = todayStats.Where(s => s.Status == AttendanceStatus.Late).Sum(s => s.Count);
        vm.RecordedToday = todayStats.Sum(s => s.Count);
        vm.AttendanceRateToday = vm.RecordedToday > 0
            ? Math.Round((double)vm.PresentToday / vm.RecordedToday * 100, 1)
            : 0;

        // ---------- المالية ----------
        vm.CollectedThisMonth = await _db.Payments
            .Where(p => !p.IsCancelled && p.PaymentDate >= monthStart && p.PaymentDate <= today)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var invoiceQuery = year is null ? _db.Invoices : _db.Invoices.Where(i => i.AcademicYearId == year.Id);
        vm.ExpectedTotal = await invoiceQuery.Where(i => i.Status != InvoiceStatus.Cancelled)
            .SumAsync(i => (decimal?)i.NetAmount) ?? 0m;
        vm.CollectedTotal = await invoiceQuery.Where(i => i.Status != InvoiceStatus.Cancelled)
            .SumAsync(i => (decimal?)i.PaidAmount) ?? 0m;

        var overdue = await _db.Installments
            .Where(i => i.DueDate < today && i.PaidAmount < i.Amount &&
                        i.Invoice.Status != InvoiceStatus.Cancelled)
            .Select(i => new { i.Invoice.StudentId, Remaining = i.Amount - i.PaidAmount })
            .ToListAsync();

        vm.OverdueAmount = overdue.Sum(o => o.Remaining);
        vm.OverdueStudentsCount = overdue.Select(o => o.StudentId).Distinct().Count();

        // ---------- اتجاه الحضور (آخر 7 أيام دراسية) ----------
        var since = today.AddDays(-13);
        var trendRaw = await _db.StudentAttendances
            .Where(a => a.Date >= since && a.Date <= today)
            .GroupBy(a => a.Date)
            .Select(g => new
            {
                Date = g.Key,
                Total = g.Count(),
                Present = g.Count(x => x.Status == AttendanceStatus.Present || x.Status == AttendanceStatus.Late)
            })
            .ToListAsync();

        vm.AttendanceTrend = trendRaw
            .OrderBy(t => t.Date)
            .TakeLast(7)
            .Select(t => new ChartPoint(
                t.Date.ToString("MM/dd"),
                t.Total > 0 ? Math.Round((decimal)t.Present / t.Total * 100m, 1) : 0))
            .ToList();

        // ---------- الطلاب حسب المرحلة ----------
        vm.StudentsByStage = await _db.Students
            .Where(s => s.Status == StudentStatus.Active && s.CurrentSectionId != null)
            .GroupBy(s => s.CurrentSection!.Grade.Stage.Name)
            .Select(g => new ChartPoint(g.Key, g.Count()))
            .ToListAsync();

        vm.GenderSplit = await _db.Students
            .Where(s => s.Status == StudentStatus.Active)
            .GroupBy(s => s.Gender)
            .Select(g => new ChartPoint(g.Key == Gender.Male ? "بنون" : "بنات", g.Count()))
            .ToListAsync();

        // ---------- الإيرادات آخر 6 أشهر ----------
        var revSince = new DateTime(today.Year, today.Month, 1).AddMonths(-5);
        var revRaw = await _db.Payments
            .Where(p => !p.IsCancelled && p.PaymentDate >= revSince)
            .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(x => x.Amount) })
            .ToListAsync();

        vm.RevenueByMonth = Enumerable.Range(0, 6)
            .Select(i => revSince.AddMonths(i))
            .Select(d => new ChartPoint(
                d.ToString("MM/yyyy"),
                revRaw.FirstOrDefault(r => r.Year == d.Year && r.Month == d.Month)?.Total ?? 0m))
            .ToList();

        // ---------- الاختبارات القادمة ----------
        vm.UpcomingExams = await _db.Exams
            .Where(e => e.ExamDate >= today)
            .OrderBy(e => e.ExamDate)
            .Take(6)
            .Select(e => new UpcomingExamRow
            {
                Id = e.Id,
                Title = e.Title,
                Subject = e.Subject.Name,
                Section = e.Section.Grade.Name + " - " + e.Section.Name,
                ExamDate = e.ExamDate,
                ExamType = e.ExamType
            })
            .ToListAsync();

        // ---------- الإعلانات ----------
        vm.Announcements = await _db.Announcements
            .Where(a => a.IsPublished && (a.ExpiryDate == null || a.ExpiryDate >= today))
            .OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.PublishDate)
            .Take(5)
            .Select(a => new AnnouncementRow
            {
                Id = a.Id,
                Title = a.Title,
                Body = a.Body,
                PublishDate = a.PublishDate,
                Audience = a.Audience,
                IsPinned = a.IsPinned
            })
            .ToListAsync();

        // ---------- آخر المدفوعات ----------
        if (User.Can(Permissions.DashboardFinance))
        {
            vm.RecentPayments = await _db.Payments
                .Where(p => !p.IsCancelled)
                .OrderByDescending(p => p.Id)
                .Take(6)
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
        }

        // ---------- غياب اليوم ----------
        vm.AbsentStudents = await _db.StudentAttendances
            .Where(a => a.Date == today && (a.Status == AttendanceStatus.Absent || a.Status == AttendanceStatus.Late))
            .OrderBy(a => a.Status)
            .Take(8)
            .Select(a => new AbsentStudentRow
            {
                StudentId = a.StudentId,
                StudentNo = a.Student.StudentNo,
                StudentName = a.Student.FullName,
                Section = a.Section.Grade.Name + " - " + a.Section.Name,
                Status = a.Status,
                GuardianNotified = a.GuardianNotified
            })
            .ToListAsync();

        // ---------- إشغال الشعب ----------
        vm.TopSections = await _db.Sections
            .Where(s => s.IsActive && (year == null || s.AcademicYearId == year.Id))
            .Select(s => new SectionLoadRow
            {
                SectionId = s.Id,
                Name = s.Grade.Name + " - " + s.Name,
                Students = s.Enrollments.Count(e => e.IsActive),
                Capacity = s.Capacity
            })
            .OrderByDescending(s => s.Students)
            .Take(6)
            .ToListAsync();

        return View(vm);
    }

    // ------------------------------------------------------------------
    private async Task<IActionResult> TeacherDashboardAsync()
    {
        var employeeId = await _user.GetEmployeeIdAsync();
        var year = await GetCurrentYearAsync();
        var today = DateTime.Today;

        // 0 = الأحد في تقويم المدرسة
        var dayIndex = (int)today.DayOfWeek;

        var vm = new TeacherDashboardViewModel
        {
            TeacherName = User.DisplayName()
        };

        if (employeeId is null)
        {
            Warning("حسابك غير مرتبط بسجل موظف. يرجى مراجعة مسؤول النظام.");
            return View("Teacher", vm);
        }

        var load = await _db.TeacherSubjects
            .Where(ts => ts.TeacherId == employeeId && ts.IsActive &&
                         (year == null || ts.AcademicYearId == year.Id))
            .Select(ts => new
            {
                ts.SectionId,
                SectionName = ts.Section.Grade.Name + " - " + ts.Section.Name,
                SubjectName = ts.Subject.Name,
                ts.SubjectId,
                Students = ts.Section.Enrollments.Count(e => e.IsActive)
            })
            .ToListAsync();

        vm.SectionsCount = load.Select(l => l.SectionId).Distinct().Count();
        vm.SubjectsCount = load.Select(l => l.SubjectId).Distinct().Count();
        vm.StudentsCount = load.GroupBy(l => l.SectionId).Sum(g => g.First().Students);

        vm.Sections = load
            .GroupBy(l => new { l.SectionId, l.SectionName })
            .Select(g => new TeacherSectionRow
            {
                SectionId = g.Key.SectionId,
                Name = g.Key.SectionName,
                Subject = string.Join("، ", g.Select(x => x.SubjectName).Distinct()),
                Students = g.First().Students
            })
            .OrderBy(s => s.Name)
            .ToList();

        vm.TodayLessons = await _db.TimetableSlots
            .Where(t => t.TeacherId == employeeId && t.DayOfWeek == dayIndex &&
                        (year == null || t.AcademicYearId == year.Id))
            .OrderBy(t => t.PeriodNo)
            .Select(t => new TodayLessonRow
            {
                PeriodNo = t.PeriodNo,
                Subject = t.Subject.Name,
                Section = t.Section.Grade.Name + " - " + t.Section.Name,
                SectionId = t.SectionId,
                Room = t.Room ?? t.Section.Room,
                StartTime = t.StartTime,
                EndTime = t.EndTime
            })
            .ToListAsync();

        vm.PendingHomework = await _db.HomeworkSubmissions
            .CountAsync(s => s.Homework.TeacherId == employeeId &&
                             s.Status == HomeworkStatus.Submitted);

        var sectionIds = load.Select(l => l.SectionId).Distinct().ToList();
        vm.UpcomingExams = await _db.Exams
            .Where(e => e.ExamDate >= today && sectionIds.Contains(e.SectionId))
            .OrderBy(e => e.ExamDate)
            .Take(5)
            .Select(e => new UpcomingExamRow
            {
                Id = e.Id,
                Title = e.Title,
                Subject = e.Subject.Name,
                Section = e.Section.Grade.Name + " - " + e.Section.Name,
                ExamDate = e.ExamDate,
                ExamType = e.ExamType
            })
            .ToListAsync();

        vm.Announcements = await _db.Announcements
            .Where(a => a.IsPublished && (a.ExpiryDate == null || a.ExpiryDate >= today) &&
                        (a.Audience == AnnouncementAudience.All || a.Audience == AnnouncementAudience.Teachers))
            .OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.PublishDate)
            .Take(5)
            .Select(a => new AnnouncementRow
            {
                Id = a.Id,
                Title = a.Title,
                Body = a.Body,
                PublishDate = a.PublishDate,
                Audience = a.Audience,
                IsPinned = a.IsPinned
            })
            .ToListAsync();

        return View("Teacher", vm);
    }
}
