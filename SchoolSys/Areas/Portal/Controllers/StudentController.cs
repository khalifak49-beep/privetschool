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
[Authorize(Roles = RoleNames.Student + "," + RoleNames.SuperAdmin)]
public class StudentController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IFileStorageService _files;

    public StudentController(ApplicationDbContext db, ICurrentUserService user, IFileStorageService files)
    {
        _db = db;
        _user = user;
        _files = files;
    }

    private async Task<int?> MyStudentIdAsync() => await _user.GetStudentIdAsync();

    public async Task<IActionResult> Index()
    {
        var studentId = await MyStudentIdAsync();
        if (studentId is null)
        {
            Warning("حسابك غير مرتبط بسجل طالب. يرجى مراجعة إدارة المدرسة.");
            return View(new StudentPortalViewModel { Student = new Student() });
        }

        var settings = await GetSettingsAsync();
        var term = await GetCurrentTermAsync();
        var year = await GetCurrentYearAsync();
        var today = DateTime.Today;

        var student = await _db.Students
            .Include(s => s.CurrentSection).ThenInclude(sec => sec!.Grade)
            .Include(s => s.CurrentSection).ThenInclude(sec => sec!.HomeroomTeacher)
            .FirstAsync(s => s.Id == studentId);

        var vm = new StudentPortalViewModel
        {
            Student = student,
            SectionName = student.CurrentSection is null ? null
                : $"{student.CurrentSection.Grade.Name} - {student.CurrentSection.Name}",
            HomeroomTeacher = student.CurrentSection?.HomeroomTeacher?.FullName,
            Currency = settings.Currency,
            TermName = term?.Name
        };

        // ---------- الحضور ----------
        var att = await _db.StudentAttendances.AsNoTracking()
            .Where(a => a.StudentId == studentId)
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
                .Where(a => a.StudentId == studentId)
                .OrderByDescending(a => a.Date).Take(10).ToListAsync()
        };

        // ---------- النتائج ----------
        vm.RecentResults = await _db.ExamResults.AsNoTracking()
            .Where(r => r.StudentId == studentId &&
                        (r.Exam.Status == ExamStatus.Graded || r.Exam.Status == ExamStatus.Approved))
            .OrderByDescending(r => r.Exam.ExamDate)
            .Take(10)
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

        if (vm.RecentResults.Count > 0)
            vm.AverageScore = Math.Round(vm.RecentResults.Where(r => !r.IsAbsent).Select(r => r.Percentage)
                .DefaultIfEmpty(0).Average(), 1);

        // ---------- الترتيب ----------
        if (student.CurrentSectionId.HasValue && term is not null)
        {
            var totals = await _db.ExamResults.AsNoTracking()
                .Where(r => r.Exam.SectionId == student.CurrentSectionId && r.Exam.TermId == term.Id &&
                            (r.Exam.Status == ExamStatus.Graded || r.Exam.Status == ExamStatus.Approved))
                .GroupBy(r => r.StudentId)
                .Select(g => new
                {
                    StudentId = g.Key,
                    Score = g.Sum(x => x.Score ?? 0m),
                    Max = g.Sum(x => x.Exam.MaxScore)
                })
                .ToListAsync();

            var ranked = totals
                .Select(t => new { t.StudentId, Pct = t.Max > 0 ? t.Score / t.Max : 0 })
                .OrderByDescending(t => t.Pct)
                .ToList();

            vm.ClassSize = ranked.Count;
            vm.Rank = ranked.FindIndex(r => r.StudentId == studentId) + 1;
        }

        // ---------- الواجبات ----------
        vm.Homework = await BuildHomeworkAsync(studentId.Value, 8);
        vm.PendingHomework = vm.Homework.Count(h => h.Status == HomeworkStatus.NotSubmitted);

        // ---------- الجدول اليوم ----------
        if (student.CurrentSectionId.HasValue)
        {
            var dayIndex = (int)today.DayOfWeek;
            vm.TodayLessons = await _db.TimetableSlots.AsNoTracking()
                .Where(t => t.SectionId == student.CurrentSectionId && t.DayOfWeek == dayIndex &&
                            (year == null || t.AcademicYearId == year.Id))
                .OrderBy(t => t.PeriodNo)
                .Select(t => new TodayLessonRow
                {
                    PeriodNo = t.PeriodNo,
                    Subject = t.Subject.Name,
                    Section = t.Teacher.FullName,
                    Room = t.Room,
                    StartTime = t.StartTime,
                    EndTime = t.EndTime
                })
                .ToListAsync();

            vm.UpcomingExams = await _db.Exams.AsNoTracking()
                .Where(e => e.SectionId == student.CurrentSectionId && e.ExamDate >= today &&
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

        // ---------- الرسوم ----------
        vm.Outstanding = await _db.Invoices.AsNoTracking()
            .Where(i => i.StudentId == studentId && i.Status != InvoiceStatus.Cancelled)
            .SumAsync(i => (decimal?)(i.NetAmount - i.PaidAmount)) ?? 0m;

        // ---------- الإعلانات ----------
        vm.Announcements = await _db.Announcements.AsNoTracking()
            .Where(a => a.IsPublished && a.PublishDate <= today &&
                        (a.ExpiryDate == null || a.ExpiryDate >= today) &&
                        (a.Audience == AnnouncementAudience.All || a.Audience == AnnouncementAudience.Students))
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

        return View(vm);
    }

    private async Task<List<PortalHomeworkRow>> BuildHomeworkAsync(int studentId, int take)
    {
        return await _db.HomeworkSubmissions.AsNoTracking()
            .Where(s => s.StudentId == studentId && s.Homework.IsPublished)
            .OrderByDescending(s => s.Homework.DueDate)
            .Take(take)
            .Select(s => new PortalHomeworkRow
            {
                HomeworkId = s.HomeworkId,
                SubmissionId = s.Id,
                Title = s.Homework.Title,
                Subject = s.Homework.Subject.Name,
                Teacher = s.Homework.Teacher.FullName,
                Description = s.Homework.Description,
                DueDate = s.Homework.DueDate,
                MaxScore = s.Homework.MaxScore,
                AttachmentPath = s.Homework.AttachmentPath,
                Status = s.Status,
                Score = s.Score,
                Feedback = s.Feedback,
                SubmissionFile = s.FilePath,
                AnswerText = s.AnswerText,
                SubmittedAt = s.SubmittedAt
            })
            .ToListAsync();
    }

    public async Task<IActionResult> Homework()
    {
        var studentId = await MyStudentIdAsync();
        if (studentId is null) return RedirectToAction(nameof(Index));

        ViewBag.Currency = (await GetSettingsAsync()).Currency;
        return View(await BuildHomeworkAsync(studentId.Value, 100));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.HomeworkSubmit)]
    public async Task<IActionResult> SubmitHomework(int submissionId, string? answerText, IFormFile? file)
    {
        var studentId = await MyStudentIdAsync();
        if (studentId is null) return Forbid();

        var sub = await _db.HomeworkSubmissions
            .Include(s => s.Homework)
            .FirstOrDefaultAsync(s => s.Id == submissionId && s.StudentId == studentId);

        if (sub is null) return NotFound();

        if (sub.Status == HomeworkStatus.Graded)
        {
            Warning("تم تصحيح هذا الواجب ولا يمكن إعادة تسليمه.");
            return RedirectToAction(nameof(Homework));
        }

        if (string.IsNullOrWhiteSpace(answerText) && (file is null || file.Length == 0))
        {
            Error("الرجاء كتابة الإجابة أو إرفاق ملف.");
            return RedirectToAction(nameof(Homework));
        }

        try
        {
            var path = await _files.SaveAsync(file, $"homework/{sub.HomeworkId}");
            if (path is not null)
            {
                _files.Delete(sub.FilePath);
                sub.FilePath = path;
            }
        }
        catch (InvalidOperationException ex)
        {
            Error(ex.Message);
            return RedirectToAction(nameof(Homework));
        }

        sub.AnswerText = answerText;
        sub.SubmittedAt = DateTime.Now;
        sub.Status = DateTime.Today > sub.Homework.DueDate.Date
            ? HomeworkStatus.Late
            : HomeworkStatus.Submitted;

        await _db.SaveChangesAsync();

        Success(sub.Status == HomeworkStatus.Late
            ? "تم تسليم الواجب متأخراً."
            : "تم تسليم الواجب بنجاح.");

        return RedirectToAction(nameof(Homework));
    }

    public async Task<IActionResult> Attendance()
    {
        var studentId = await MyStudentIdAsync();
        if (studentId is null) return RedirectToAction(nameof(Index));

        var records = await _db.StudentAttendances.AsNoTracking()
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.Date)
            .Take(120)
            .ToListAsync();

        return View(records);
    }

    public async Task<IActionResult> Grades()
    {
        var studentId = await MyStudentIdAsync();
        if (studentId is null) return RedirectToAction(nameof(Index));

        return RedirectToAction("ReportCard", "Results", new { area = "", studentId });
    }

    /// <summary>
    /// جدول الطالب داخل بوابته. لا يُوجَّه إلى الشاشة الإدارية لأنها تسمح
    /// باستعراض جدول أي شعبة أخرى.
    /// </summary>
    public async Task<IActionResult> Timetable()
    {
        var studentId = await MyStudentIdAsync();
        if (studentId is null) return RedirectToAction(nameof(Index));

        var year = await GetCurrentYearAsync();
        var student = await _db.Students.AsNoTracking()
            .Include(s => s.CurrentSection).ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        var vm = new TimetableViewModel
        {
            Mode = "section",
            SectionId = student?.CurrentSectionId,
            Title = student?.CurrentSection is null
                ? null
                : $"{student.CurrentSection.Grade.Name} - {student.CurrentSection.Name}"
        };

        if (student?.CurrentSectionId is null) return View(vm);

        var slots = await _db.TimetableSlots.AsNoTracking()
            .Where(t => t.SectionId == student.CurrentSectionId &&
                        (year == null || t.AcademicYearId == year.Id))
            .Select(t => new
            {
                t.Id,
                t.DayOfWeek,
                t.PeriodNo,
                Subject = t.Subject.Name,
                Teacher = t.Teacher.FullName,
                Room = t.Room ?? t.Section.Room,
                t.StartTime,
                t.EndTime
            })
            .ToListAsync();

        foreach (var s in slots)
        {
            vm.Cells[(s.DayOfWeek, s.PeriodNo)] = new TimetableCell
            {
                Id = s.Id,
                Subject = s.Subject,
                Teacher = s.Teacher,
                Room = s.Room,
                StartTime = s.StartTime,
                EndTime = s.EndTime
            };
        }

        vm.MaxPeriods = slots.Count > 0 ? Math.Max(6, slots.Max(s => s.PeriodNo)) : 6;

        vm.PeriodTimes = slots
            .GroupBy(s => s.PeriodNo)
            .OrderBy(g => g.Key)
            .Select(g => new PeriodTime(g.Key, g.First().StartTime, g.First().EndTime))
            .ToList();

        return View(vm);
    }
}
