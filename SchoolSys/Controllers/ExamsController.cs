using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[HasPermission(Permissions.ExamsView)]
public class ExamsController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;

    public ExamsController(ApplicationDbContext db, ICurrentUserService user,
        IAuditService audit, INotificationService notify)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _notify = notify;
    }

    /// <summary>معرّفات الشعب التي يحق للمستخدم الحالي رؤيتها (المعلم يرى شعبه فقط).</summary>
    private async Task<List<int>?> MySectionIdsAsync()
    {
        if (!User.IsInRole(RoleNames.Teacher) || User.Can(Permissions.ExamsApprove))
            return null;   // بلا تقييد

        var employeeId = await _user.GetEmployeeIdAsync();
        var year = await GetCurrentYearAsync();

        return await _db.TeacherSubjects
            .Where(ts => ts.TeacherId == employeeId && (year == null || ts.AcademicYearId == year.Id))
            .Select(ts => ts.SectionId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IActionResult> Index(int? sectionId, int? subjectId, int? termId,
        ExamStatus? status, string? q, int page = 1)
    {
        var year = await GetCurrentYearAsync();
        var mine = await MySectionIdsAsync();

        var vm = new ExamIndexViewModel
        {
            SectionId = sectionId,
            SubjectId = subjectId,
            TermId = termId,
            Status = status,
            Q = q,
            Page = page
        };

        var query = _db.Exams.AsNoTracking()
            .Where(e => year == null || e.Term.AcademicYearId == year.Id);

        if (mine is not null) query = query.Where(e => mine.Contains(e.SectionId));
        if (sectionId.HasValue) query = query.Where(e => e.SectionId == sectionId);
        if (subjectId.HasValue) query = query.Where(e => e.SubjectId == subjectId);
        if (termId.HasValue) query = query.Where(e => e.TermId == termId);
        if (status.HasValue) query = query.Where(e => e.Status == status);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(e => e.Title.Contains(q.Trim()));

        var projected = query.Select(e => new ExamListRow
        {
            Id = e.Id,
            Title = e.Title,
            ExamType = e.ExamType,
            Subject = e.Subject.Name,
            Section = e.Section.Grade.Name + " - " + e.Section.Name,
            Term = e.Term.Name,
            ExamDate = e.ExamDate,
            MaxScore = e.MaxScore,
            Status = e.Status,
            StudentsCount = e.Section.Enrollments.Count(x => x.IsActive),
            MarkedCount = e.Results.Count(r => r.Score != null || r.IsAbsent),
            Average = e.Results.Where(r => r.Score != null).Average(r => r.Score)
        });

        vm.Exams = await PagedList<ExamListRow>.CreateAsync(
            projected.OrderByDescending(e => e.ExamDate).ThenBy(e => e.Section), page, 20);

        vm.TotalExams = await query.CountAsync();
        vm.UpcomingExams = await query.CountAsync(e => e.ExamDate >= DateTime.Today);
        vm.PendingMarks = await query.CountAsync(e =>
            e.ExamDate < DateTime.Today && e.Status != ExamStatus.Graded && e.Status != ExamStatus.Approved);

        await FillListsAsync(vm, year, mine);
        return View(vm);
    }

    private async Task FillListsAsync(ExamIndexViewModel vm, AcademicYear? year, List<int>? mine)
    {
        var sections = _db.Sections.AsNoTracking()
            .Where(s => s.IsActive && (year == null || s.AcademicYearId == year.Id));
        if (mine is not null) sections = sections.Where(s => mine.Contains(s.Id));

        vm.Sections = await sections.OrderBy(s => s.Grade.SeqNo).ThenBy(s => s.Name)
            .Select(s => new SelectListItem(s.Grade.Name + " - " + s.Name, s.Id.ToString(), s.Id == vm.SectionId))
            .ToListAsync();

        vm.Subjects = await _db.Subjects.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString(), s.Id == vm.SubjectId)).ToListAsync();

        vm.Terms = await _db.Terms.AsNoTracking()
            .Where(t => year == null || t.AcademicYearId == year.Id)
            .OrderBy(t => t.SeqNo)
            .Select(t => new SelectListItem(t.Name, t.Id.ToString(), t.Id == vm.TermId)).ToListAsync();
    }

    // ==================================================================
    [HasPermission(Permissions.ExamsManage)]
    public async Task<IActionResult> Create(int? sectionId)
    {
        var term = await GetCurrentTermAsync();
        var vm = new ExamFormViewModel
        {
            SectionId = sectionId,
            TermId = term?.Id
        };
        await FillFormListsAsync(vm);
        return View("Form", vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.ExamsManage)]
    public async Task<IActionResult> Create(ExamFormViewModel vm)
    {
        if (vm.PassScore > vm.MaxScore)
            ModelState.AddModelError(nameof(vm.PassScore), "درجة النجاح لا يمكن أن تتجاوز الدرجة العظمى.");

        if (!ModelState.IsValid)
        {
            await FillFormListsAsync(vm);
            return View("Form", vm);
        }

        var exam = new Exam
        {
            Title = vm.Title.Trim(),
            ExamType = vm.ExamType,
            SubjectId = vm.SubjectId!.Value,
            SectionId = vm.SectionId!.Value,
            TermId = vm.TermId!.Value,
            ExamDate = vm.ExamDate,
            StartTime = vm.StartTime,
            DurationMinutes = vm.DurationMinutes,
            MaxScore = vm.MaxScore,
            PassScore = vm.PassScore,
            Weight = vm.Weight,
            Status = vm.Status,
            Notes = vm.Notes,
            CreatedByUserId = _user.UserId
        };

        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        // إنشاء سجلات فارغة لطلاب الشعبة
        var studentIds = await _db.Students
            .Where(s => s.CurrentSectionId == exam.SectionId && s.Status == StudentStatus.Active)
            .Select(s => s.Id).ToListAsync();

        _db.ExamResults.AddRange(studentIds.Select(id => new ExamResult { ExamId = exam.Id, StudentId = id }));
        await _db.SaveChangesAsync();

        await _audit.LogAsync("إنشاء اختبار", nameof(Exam), exam.Id, exam.Title);

        if (exam.Status == ExamStatus.Published)
            await NotifyExamPublishedAsync(exam);

        Success($"تم إنشاء الاختبار «{exam.Title}» مع {studentIds.Count} طالب.");
        return RedirectToAction(nameof(Marks), new { id = exam.Id });
    }

    [HasPermission(Permissions.ExamsManage)]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await _db.Exams.FindAsync(id);
        if (e is null) return NotFound();

        var vm = new ExamFormViewModel
        {
            Id = e.Id,
            Title = e.Title,
            ExamType = e.ExamType,
            SubjectId = e.SubjectId,
            SectionId = e.SectionId,
            TermId = e.TermId,
            ExamDate = e.ExamDate,
            StartTime = e.StartTime,
            DurationMinutes = e.DurationMinutes,
            MaxScore = e.MaxScore,
            PassScore = e.PassScore,
            Weight = e.Weight,
            Status = e.Status,
            Notes = e.Notes
        };

        await FillFormListsAsync(vm);
        return View("Form", vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.ExamsManage)]
    public async Task<IActionResult> Edit(ExamFormViewModel vm)
    {
        if (vm.PassScore > vm.MaxScore)
            ModelState.AddModelError(nameof(vm.PassScore), "درجة النجاح لا يمكن أن تتجاوز الدرجة العظمى.");

        if (!ModelState.IsValid)
        {
            await FillFormListsAsync(vm);
            return View("Form", vm);
        }

        var e = await _db.Exams.FindAsync(vm.Id);
        if (e is null) return NotFound();

        var wasPublished = e.Status == ExamStatus.Published;

        e.Title = vm.Title.Trim();
        e.ExamType = vm.ExamType;
        e.SubjectId = vm.SubjectId!.Value;
        e.TermId = vm.TermId!.Value;
        e.ExamDate = vm.ExamDate;
        e.StartTime = vm.StartTime;
        e.DurationMinutes = vm.DurationMinutes;
        e.MaxScore = vm.MaxScore;
        e.PassScore = vm.PassScore;
        e.Weight = vm.Weight;
        e.Status = vm.Status;
        e.Notes = vm.Notes;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("تعديل اختبار", nameof(Exam), e.Id, e.Title);

        if (!wasPublished && e.Status == ExamStatus.Published)
            await NotifyExamPublishedAsync(e);

        Success("تم حفظ بيانات الاختبار.");
        return RedirectToAction(nameof(Index));
    }

    private async Task FillFormListsAsync(ExamFormViewModel vm)
    {
        var year = await GetCurrentYearAsync();
        var mine = await MySectionIdsAsync();

        var sections = _db.Sections.AsNoTracking()
            .Where(s => s.IsActive && (year == null || s.AcademicYearId == year.Id));
        if (mine is not null) sections = sections.Where(s => mine.Contains(s.Id));

        vm.Sections = await sections.OrderBy(s => s.Grade.SeqNo).ThenBy(s => s.Name)
            .Select(s => new SelectListItem(s.Grade.Name + " - " + s.Name, s.Id.ToString(), s.Id == vm.SectionId))
            .ToListAsync();

        vm.Subjects = await _db.Subjects.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString(), s.Id == vm.SubjectId)).ToListAsync();

        vm.Terms = await _db.Terms.AsNoTracking()
            .Where(t => year == null || t.AcademicYearId == year.Id).OrderBy(t => t.SeqNo)
            .Select(t => new SelectListItem(t.Name, t.Id.ToString(), t.Id == vm.TermId)).ToListAsync();
    }

    private async Task NotifyExamPublishedAsync(Exam exam)
    {
        var info = await _db.Exams.Where(e => e.Id == exam.Id)
            .Select(e => new
            {
                Subject = e.Subject.Name,
                Section = e.Section.Grade.Name + " - " + e.Section.Name
            }).FirstAsync();

        var studentIds = await _db.Students
            .Where(s => s.CurrentSectionId == exam.SectionId && s.Status == StudentStatus.Active)
            .Select(s => s.Id).ToListAsync();

        var userIds = await _db.Users
            .Where(u => u.StudentId != null && studentIds.Contains(u.StudentId.Value))
            .Select(u => u.Id).ToListAsync();

        if (userIds.Count > 0)
            await _notify.NotifyUsersAsync(userIds, "اختبار جديد",
                $"{exam.Title} — {info.Subject} بتاريخ {exam.ExamDate:yyyy/MM/dd}",
                NotificationType.Grades, NotificationSeverity.Info, $"/Exams");
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.ExamsManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var exam = await _db.Exams.FindAsync(id);
        if (exam is null) return NotFound();

        if (exam.Status == ExamStatus.Approved)
        {
            Error("لا يمكن حذف اختبار معتمد.");
            return RedirectToAction(nameof(Index));
        }

        _db.Exams.Remove(exam);   // النتائج تُحذف تلقائياً
        await _db.SaveChangesAsync();
        await _audit.LogAsync("حذف اختبار", nameof(Exam), id, exam.Title);

        Success("تم حذف الاختبار.");
        return RedirectToAction(nameof(Index));
    }

    // ==================================================================
    // رصد الدرجات
    // ==================================================================
    [HasPermission(Permissions.ExamsView)]
    public async Task<IActionResult> Marks(int id)
    {
        var exam = await _db.Exams
            .Include(e => e.Subject)
            .Include(e => e.Section).ThenInclude(s => s.Grade)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (exam is null) return NotFound();

        var mine = await MySectionIdsAsync();
        if (mine is not null && !mine.Contains(exam.SectionId)) return Forbid();

        var vm = new ExamMarksViewModel
        {
            ExamId = exam.Id,
            Title = exam.Title,
            Subject = exam.Subject.Name,
            Section = $"{exam.Section.Grade.Name} - {exam.Section.Name}",
            ExamDate = exam.ExamDate,
            MaxScore = exam.MaxScore,
            PassScore = exam.PassScore,
            Status = exam.Status
        };

        // مزامنة الطلاب الجدد الذين لم تُنشأ لهم سجلات
        var studentIds = await _db.Students
            .Where(s => s.CurrentSectionId == exam.SectionId && s.Status == StudentStatus.Active)
            .Select(s => s.Id).ToListAsync();

        var existingIds = await _db.ExamResults.Where(r => r.ExamId == id)
            .Select(r => r.StudentId).ToListAsync();

        var missing = studentIds.Except(existingIds).ToList();
        if (missing.Count > 0)
        {
            _db.ExamResults.AddRange(missing.Select(sid => new ExamResult { ExamId = id, StudentId = sid }));
            await _db.SaveChangesAsync();
        }

        vm.Entries = await _db.ExamResults.AsNoTracking()
            .Where(r => r.ExamId == id)
            .OrderBy(r => r.Student.FullName)
            .Select(r => new MarkEntry
            {
                ResultId = r.Id,
                StudentId = r.StudentId,
                StudentNo = r.Student.StudentNo,
                StudentName = r.Student.FullName,
                PhotoPath = r.Student.PhotoPath,
                Score = r.Score,
                IsAbsent = r.IsAbsent,
                Notes = r.Notes
            })
            .ToListAsync();

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.ExamsEnterMarks)]
    public async Task<IActionResult> SaveMarks(int examId, List<MarkEntry> entries, bool markGraded = false)
    {
        var exam = await _db.Exams.FindAsync(examId);
        if (exam is null) return NotFound();

        if (exam.Status == ExamStatus.Approved)
        {
            Error("الاختبار معتمد ولا يمكن تعديل درجاته.");
            return RedirectToAction(nameof(Marks), new { id = examId });
        }

        var mine = await MySectionIdsAsync();
        if (mine is not null && !mine.Contains(exam.SectionId)) return Forbid();

        var results = await _db.ExamResults.Where(r => r.ExamId == examId).ToDictionaryAsync(r => r.StudentId);
        var invalid = 0;
        var userId = _user.UserId;

        foreach (var entry in entries)
        {
            if (!results.TryGetValue(entry.StudentId, out var r)) continue;

            if (entry.Score.HasValue && (entry.Score < 0 || entry.Score > exam.MaxScore))
            {
                invalid++;
                continue;
            }

            r.Score = entry.IsAbsent ? null : entry.Score;
            r.IsAbsent = entry.IsAbsent;
            r.Notes = entry.Notes;
            r.EnteredByUserId = userId;
            r.EnteredAt = DateTime.Now;
        }

        if (markGraded && exam.Status is ExamStatus.Draft or ExamStatus.Published)
            exam.Status = ExamStatus.Graded;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("رصد درجات", nameof(Exam), examId, exam.Title);

        if (invalid > 0)
            Warning($"تم تجاهل {invalid} درجة خارج النطاق المسموح (0 – {exam.MaxScore:0.##}).");

        Success("تم حفظ الدرجات بنجاح.");
        return RedirectToAction(nameof(Marks), new { id = examId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.ExamsApprove)]
    public async Task<IActionResult> Approve(int id)
    {
        var exam = await _db.Exams
            .Include(e => e.Subject)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (exam is null) return NotFound();

        var pending = await _db.ExamResults.CountAsync(r => r.ExamId == id && r.Score == null && !r.IsAbsent);
        if (pending > 0)
        {
            Error($"لا يمكن الاعتماد: ما زال هناك {pending} طالب بلا درجة.");
            return RedirectToAction(nameof(Marks), new { id });
        }

        exam.Status = ExamStatus.Approved;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("اعتماد نتائج اختبار", nameof(Exam), id, exam.Title);

        // إشعار أولياء الأمور بالنتائج
        var results = await _db.ExamResults
            .Where(r => r.ExamId == id)
            .Select(r => new { r.StudentId, r.Score, r.IsAbsent })
            .ToListAsync();

        foreach (var r in results.Where(x => !x.IsAbsent && x.Score.HasValue))
        {
            await _notify.NotifyGuardiansOfStudentAsync(r.StudentId,
                $"نتيجة {exam.Subject.Name}",
                $"{exam.Title}: {r.Score:0.##} من {exam.MaxScore:0.##}",
                NotificationType.Grades,
                r.Score >= exam.PassScore ? NotificationSeverity.Success : NotificationSeverity.Warning,
                alsoExternal: false);
        }

        Success("تم اعتماد نتائج الاختبار وإشعار أولياء الأمور.");
        return RedirectToAction(nameof(Marks), new { id });
    }
}
