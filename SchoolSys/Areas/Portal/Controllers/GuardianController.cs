using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Areas.Portal.ViewModels;
using SchoolSys.Controllers;
using SchoolSys.Data;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using SchoolSys.ViewModels;

namespace SchoolSys.Areas.Portal.Controllers;

[Area("Portal")]
[Authorize(Roles = RoleNames.Guardian + "," + RoleNames.SuperAdmin)]
public class GuardianController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;

    public GuardianController(ApplicationDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    private async Task<List<int>> MyChildrenIdsAsync()
    {
        var guardianId = await _user.GetGuardianIdAsync();
        if (guardianId is null) return [];

        return await _db.StudentGuardians
            .Where(sg => sg.GuardianId == guardianId)
            .Select(sg => sg.StudentId)
            .ToListAsync();
    }

    public async Task<IActionResult> Index()
    {
        var settings = await GetSettingsAsync();
        var today = DateTime.Today;
        var term = await GetCurrentTermAsync();

        var vm = new GuardianPortalViewModel
        {
            GuardianName = User.DisplayName(),
            Currency = settings.Currency
        };

        var childIds = await MyChildrenIdsAsync();
        if (childIds.Count == 0)
        {
            Warning("حسابك غير مرتبط بأي طالب. يرجى مراجعة إدارة المدرسة.");
            return View(vm);
        }

        var children = await _db.Students.AsNoTracking()
            .Where(s => childIds.Contains(s.Id))
            .Select(s => new ChildCard
            {
                StudentId = s.Id,
                FullName = s.FullName,
                StudentNo = s.StudentNo,
                PhotoPath = s.PhotoPath,
                Status = s.Status,
                Section = s.CurrentSection != null
                    ? s.CurrentSection.Grade.Name + " - " + s.CurrentSection.Name : null,
                Outstanding = s.Invoices.Where(i => i.Status != InvoiceStatus.Cancelled)
                    .Sum(i => (decimal?)(i.NetAmount - i.PaidAmount)) ?? 0m,
                InvoiceId = s.Invoices.Where(i => i.Status != InvoiceStatus.Cancelled)
                    .OrderByDescending(i => i.IssueDate).Select(i => (int?)i.Id).FirstOrDefault(),
                TransportRoute = s.Id > 0
                    ? _db.StudentTransports.Where(t => t.StudentId == s.Id && t.IsActive)
                        .Select(t => t.Route.Name).FirstOrDefault() : null,
                TransportStop = _db.StudentTransports.Where(t => t.StudentId == s.Id && t.IsActive)
                    .Select(t => t.Stop != null ? t.Stop.Name : null).FirstOrDefault()
            })
            .ToListAsync();

        // ---------- الحضور ----------
        var attendance = await _db.StudentAttendances.AsNoTracking()
            .Where(a => childIds.Contains(a.StudentId))
            .GroupBy(a => a.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Total = g.Count(),
                Present = g.Count(x => x.Status == AttendanceStatus.Present || x.Status == AttendanceStatus.Late),
                Absent = g.Count(x => x.Status == AttendanceStatus.Absent),
                Late = g.Count(x => x.Status == AttendanceStatus.Late)
            })
            .ToListAsync();

        var absentToday = await _db.StudentAttendances.AsNoTracking()
            .Where(a => childIds.Contains(a.StudentId) && a.Date == today && a.Status == AttendanceStatus.Absent)
            .Select(a => a.StudentId)
            .ToListAsync();

        // ---------- المتأخرات ----------
        var overdue = await _db.Installments.AsNoTracking()
            .Where(i => childIds.Contains(i.Invoice.StudentId) && i.DueDate < today &&
                        i.PaidAmount < i.Amount && i.Invoice.Status != InvoiceStatus.Cancelled)
            .GroupBy(i => i.Invoice.StudentId)
            .Select(g => new { StudentId = g.Key, Amount = g.Sum(x => x.Amount - x.PaidAmount) })
            .ToListAsync();

        // ---------- الواجبات ----------
        var pendingHw = await _db.HomeworkSubmissions.AsNoTracking()
            .Where(s => childIds.Contains(s.StudentId) && s.Status == HomeworkStatus.NotSubmitted &&
                        s.Homework.IsPublished && s.Homework.DueDate >= today)
            .GroupBy(s => s.StudentId)
            .Select(g => new { StudentId = g.Key, Count = g.Count() })
            .ToListAsync();

        // ---------- المعدل ----------
        var results = await _db.ExamResults.AsNoTracking()
            .Where(r => childIds.Contains(r.StudentId) && !r.IsAbsent && r.Score != null &&
                        (r.Exam.Status == ExamStatus.Graded || r.Exam.Status == ExamStatus.Approved) &&
                        (term == null || r.Exam.TermId == term.Id))
            .GroupBy(r => r.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Score = g.Sum(x => x.Score!.Value),
                Max = g.Sum(x => x.Exam.MaxScore)
            })
            .ToListAsync();

        foreach (var c in children)
        {
            var a = attendance.FirstOrDefault(x => x.StudentId == c.StudentId);
            c.AttendanceRate = a is { Total: > 0 } ? Math.Round((double)a.Present / a.Total * 100, 1) : 0;
            c.AbsentDays = a?.Absent ?? 0;
            c.LateDays = a?.Late ?? 0;
            c.AbsentToday = absentToday.Contains(c.StudentId);
            c.Overdue = overdue.FirstOrDefault(x => x.StudentId == c.StudentId)?.Amount ?? 0m;
            c.PendingHomework = pendingHw.FirstOrDefault(x => x.StudentId == c.StudentId)?.Count ?? 0;

            var r = results.FirstOrDefault(x => x.StudentId == c.StudentId);
            c.AveragePercent = r is { Max: > 0 } ? Math.Round(r.Score / r.Max * 100m, 1) : 0;
        }

        vm.Children = children;

        vm.Announcements = await _db.Announcements.AsNoTracking()
            .Where(a => a.IsPublished && a.PublishDate <= today &&
                        (a.ExpiryDate == null || a.ExpiryDate >= today) &&
                        (a.Audience == AnnouncementAudience.All || a.Audience == AnnouncementAudience.Guardians))
            .OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.PublishDate)
            .Take(6)
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

        return View(vm);
    }

    public async Task<IActionResult> Child(int id)
    {
        var childIds = await MyChildrenIdsAsync();
        if (!childIds.Contains(id)) return Forbid();

        var settings = await GetSettingsAsync();
        var term = await GetCurrentTermAsync();
        var year = await GetCurrentYearAsync();

        var student = await _db.Students
            .Include(s => s.CurrentSection).ThenInclude(sec => sec!.Grade)
            .Include(s => s.CurrentSection).ThenInclude(sec => sec!.HomeroomTeacher)
            .FirstAsync(s => s.Id == id);

        var vm = new GuardianChildDetailsViewModel
        {
            Student = student,
            SectionName = student.CurrentSection is null ? null
                : $"{student.CurrentSection.Grade.Name} - {student.CurrentSection.Name}",
            HomeroomTeacher = student.CurrentSection?.HomeroomTeacher?.FullName,
            Currency = settings.Currency,
            TermName = term?.Name
        };

        // الحضور
        var att = await _db.StudentAttendances.AsNoTracking()
            .Where(a => a.StudentId == id)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        vm.Attendance = new AttendanceSummary
        {
            Present = att.FirstOrDefault(a => a.Status == AttendanceStatus.Present)?.Count ?? 0,
            Absent = att.FirstOrDefault(a => a.Status == AttendanceStatus.Absent)?.Count ?? 0,
            Late = att.FirstOrDefault(a => a.Status == AttendanceStatus.Late)?.Count ?? 0,
            Excused = att.FirstOrDefault(a => a.Status == AttendanceStatus.Excused)?.Count ?? 0,
            Recent = await _db.StudentAttendances.AsNoTracking()
                .Where(a => a.StudentId == id).OrderByDescending(a => a.Date).Take(15).ToListAsync()
        };

        // النتائج
        vm.Results = await _db.ExamResults.AsNoTracking()
            .Where(r => r.StudentId == id &&
                        (r.Exam.Status == ExamStatus.Graded || r.Exam.Status == ExamStatus.Approved))
            .OrderByDescending(r => r.Exam.ExamDate)
            .Take(25)
            .Select(r => new StudentSubjectResultRow
            {
                Subject = r.Exam.Subject.Name,
                ExamTitle = r.Exam.Title,
                ExamType = r.Exam.ExamType,
                ExamDate = r.Exam.ExamDate,
                Score = r.Score,
                MaxScore = r.Exam.MaxScore,
                IsAbsent = r.IsAbsent
            })
            .ToListAsync();

        var valid = vm.Results.Where(r => !r.IsAbsent && r.Score.HasValue).ToList();
        if (valid.Count > 0)
        {
            var totalScore = valid.Sum(r => r.Score!.Value);
            var totalMax = valid.Sum(r => r.MaxScore);
            vm.AveragePercent = totalMax > 0 ? Math.Round(totalScore / totalMax * 100m, 1) : 0;
        }

        // الواجبات
        vm.Homework = await _db.HomeworkSubmissions.AsNoTracking()
            .Where(s => s.StudentId == id && s.Homework.IsPublished)
            .OrderByDescending(s => s.Homework.DueDate)
            .Take(20)
            .Select(s => new PortalHomeworkRow
            {
                HomeworkId = s.HomeworkId,
                Title = s.Homework.Title,
                Subject = s.Homework.Subject.Name,
                Teacher = s.Homework.Teacher.FullName,
                DueDate = s.Homework.DueDate,
                MaxScore = s.Homework.MaxScore,
                Status = s.Status,
                Score = s.Score,
                Feedback = s.Feedback,
                SubmittedAt = s.SubmittedAt
            })
            .ToListAsync();

        // الملاحظات
        vm.Notes = await _db.StudentNotes.AsNoTracking()
            .Include(n => n.Employee)
            .Where(n => n.StudentId == id)
            .OrderByDescending(n => n.NoteDate)
            .Take(20)
            .ToListAsync();

        // الجدول
        if (student.CurrentSectionId.HasValue)
        {
            vm.Timetable = await _db.TimetableSlots.AsNoTracking()
                .Where(t => t.SectionId == student.CurrentSectionId &&
                            (year == null || t.AcademicYearId == year.Id))
                .OrderBy(t => t.DayOfWeek).ThenBy(t => t.PeriodNo)
                .Select(t => new TodayLessonRow
                {
                    PeriodNo = t.PeriodNo,
                    Subject = t.Subject.Name,
                    Section = t.Teacher.FullName,
                    SectionId = t.DayOfWeek,      // نستخدمها لتخزين اليوم في العرض
                    Room = t.Room,
                    StartTime = t.StartTime,
                    EndTime = t.EndTime
                })
                .ToListAsync();

            vm.UpcomingExams = await _db.Exams.AsNoTracking()
                .Where(e => e.SectionId == student.CurrentSectionId && e.ExamDate >= DateTime.Today &&
                            e.Status != ExamStatus.Draft)
                .OrderBy(e => e.ExamDate).Take(5)
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
        }

        // المالية
        var invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.Installments)
            .Where(i => i.StudentId == id && i.Status != InvoiceStatus.Cancelled)
            .OrderByDescending(i => i.IssueDate)
            .FirstOrDefaultAsync();

        if (invoice is not null)
        {
            vm.Finance = new StudentFinanceSummary
            {
                InvoiceId = invoice.Id,
                InvoiceNo = invoice.InvoiceNo,
                Total = invoice.NetAmount,
                Paid = invoice.PaidAmount,
                Overdue = invoice.Installments
                    .Where(x => x.DueDate < DateTime.Today && x.PaidAmount < x.Amount)
                    .Sum(x => x.Amount - x.PaidAmount),
                Installments = invoice.Installments.OrderBy(x => x.SeqNo).ToList()
            };
        }

        // النقل
        vm.Transport = await _db.StudentTransports.AsNoTracking()
            .Where(t => t.StudentId == id && t.IsActive)
            .Select(t => new TransportInfo
            {
                RouteName = t.Route.Name,
                StopName = t.Stop != null ? t.Stop.Name : null,
                BusNo = t.Route.Bus != null ? t.Route.Bus.BusNo : null,
                DriverName = t.Route.Bus != null && t.Route.Bus.Driver != null ? t.Route.Bus.Driver.FullName : null,
                DriverPhone = t.Route.Bus != null && t.Route.Bus.Driver != null ? t.Route.Bus.Driver.Phone : null,
                MonthlyFee = t.MonthlyFee
            })
            .FirstOrDefaultAsync();

        return View(vm);
    }

    public async Task<IActionResult> Fees()
    {
        var settings = await GetSettingsAsync();
        var childIds = await MyChildrenIdsAsync();

        var vm = new GuardianFeesViewModel { Currency = settings.Currency };

        var invoices = await _db.Invoices.AsNoTracking()
            .Include(i => i.Installments)
            .Where(i => childIds.Contains(i.StudentId) && i.Status != InvoiceStatus.Cancelled)
            .OrderByDescending(i => i.IssueDate)
            .ToListAsync();

        var studentNames = await _db.Students.AsNoTracking()
            .Where(s => childIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.FullName);

        var payments = await _db.Payments.AsNoTracking()
            .Where(p => childIds.Contains(p.StudentId) && !p.IsCancelled)
            .ToListAsync();

        vm.Invoices = invoices.Select(i => new ChildInvoice
        {
            InvoiceId = i.Id,
            StudentId = i.StudentId,
            StudentName = studentNames.GetValueOrDefault(i.StudentId, ""),
            InvoiceNo = i.InvoiceNo,
            NetAmount = i.NetAmount,
            PaidAmount = i.PaidAmount,
            Status = i.Status,
            Installments = i.Installments.OrderBy(x => x.SeqNo).ToList(),
            Payments = payments.Where(p => p.InvoiceId == i.Id)
                .OrderByDescending(p => p.PaymentDate).ToList()
        }).ToList();

        return View(vm);
    }

    public async Task<IActionResult> Attendance(int id)
    {
        var childIds = await MyChildrenIdsAsync();
        if (!childIds.Contains(id)) return Forbid();

        ViewBag.StudentName = await _db.Students.Where(s => s.Id == id)
            .Select(s => s.FullName).FirstOrDefaultAsync();
        ViewBag.StudentId = id;

        var records = await _db.StudentAttendances.AsNoTracking()
            .Where(a => a.StudentId == id)
            .OrderByDescending(a => a.Date)
            .Take(120)
            .ToListAsync();

        return View("~/Areas/Portal/Views/Student/Attendance.cshtml", records);
    }
}
