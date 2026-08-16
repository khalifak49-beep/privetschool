using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[HasPermission(Permissions.HomeworkView)]
public class HomeworkController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IFileStorageService _files;
    private readonly INotificationService _notify;

    public HomeworkController(ApplicationDbContext db, ICurrentUserService user,
        IFileStorageService files, INotificationService notify)
    {
        _db = db;
        _user = user;
        _files = files;
        _notify = notify;
    }

    private async Task<int?> MyTeacherIdAsync()
        => User.IsInRole(RoleNames.Teacher) && !User.Can(Permissions.AcademicManage)
            ? await _user.GetEmployeeIdAsync()
            : null;

    public async Task<IActionResult> Index(int? sectionId, int? subjectId, string? q, int page = 1)
    {
        var year = await GetCurrentYearAsync();
        var teacherId = await MyTeacherIdAsync();

        var vm = new HomeworkIndexViewModel
        {
            SectionId = sectionId,
            SubjectId = subjectId,
            Q = q,
            Page = page
        };

        var query = _db.Homeworks.AsNoTracking()
            .Where(h => year == null || h.Term.AcademicYearId == year.Id);

        if (teacherId is not null) query = query.Where(h => h.TeacherId == teacherId);
        if (sectionId.HasValue) query = query.Where(h => h.SectionId == sectionId);
        if (subjectId.HasValue) query = query.Where(h => h.SubjectId == subjectId);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(h => h.Title.Contains(q.Trim()));

        var projected = query.Select(h => new HomeworkListRow
        {
            Id = h.Id,
            Title = h.Title,
            Subject = h.Subject.Name,
            Section = h.Section.Grade.Name + " - " + h.Section.Name,
            Teacher = h.Teacher.FullName,
            AssignedDate = h.AssignedDate,
            DueDate = h.DueDate,
            MaxScore = h.MaxScore,
            IsPublished = h.IsPublished,
            Total = h.Submissions.Count,
            Submitted = h.Submissions.Count(s => s.Status != HomeworkStatus.NotSubmitted),
            Graded = h.Submissions.Count(s => s.Score != null)
        });

        vm.Items = await PagedList<HomeworkListRow>.CreateAsync(
            projected.OrderByDescending(h => h.DueDate), page, 20);

        vm.ActiveCount = await query.CountAsync(h => h.DueDate >= DateTime.Today);
        vm.PendingGrading = await _db.HomeworkSubmissions
            .CountAsync(s => s.Status == HomeworkStatus.Submitted &&
                             (teacherId == null || s.Homework.TeacherId == teacherId));

        var sections = _db.Sections.AsNoTracking()
            .Where(s => s.IsActive && (year == null || s.AcademicYearId == year.Id));

        if (teacherId is not null)
        {
            var mine = await _db.TeacherSubjects
                .Where(ts => ts.TeacherId == teacherId && (year == null || ts.AcademicYearId == year.Id))
                .Select(ts => ts.SectionId).Distinct().ToListAsync();
            sections = sections.Where(s => mine.Contains(s.Id));
        }

        vm.Sections = await sections.OrderBy(s => s.Grade.SeqNo).ThenBy(s => s.Name)
            .Select(s => new SelectListItem(s.Grade.Name + " - " + s.Name, s.Id.ToString(), s.Id == sectionId))
            .ToListAsync();

        vm.Subjects = await _db.Subjects.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString(), s.Id == subjectId)).ToListAsync();

        return View(vm);
    }

    [HasPermission(Permissions.HomeworkManage)]
    public async Task<IActionResult> Create()
    {
        var term = await GetCurrentTermAsync();
        var vm = new HomeworkFormViewModel
        {
            TermId = term?.Id,
            TeacherId = await _user.GetEmployeeIdAsync()
        };
        await FillListsAsync(vm);
        return View("Form", vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.HomeworkManage)]
    public async Task<IActionResult> Create(HomeworkFormViewModel vm)
    {
        if (vm.DueDate < vm.AssignedDate)
            ModelState.AddModelError(nameof(vm.DueDate), "تاريخ التسليم يجب أن يكون بعد تاريخ التكليف.");

        if (!ModelState.IsValid)
        {
            await FillListsAsync(vm);
            return View("Form", vm);
        }

        var teacherId = vm.TeacherId ?? await _user.GetEmployeeIdAsync();
        if (teacherId is null)
        {
            Error("حسابك غير مرتبط بسجل معلم. يرجى اختيار المعلم أو مراجعة مسؤول النظام.");
            await FillListsAsync(vm);
            return View("Form", vm);
        }

        var homework = new Homework
        {
            Title = vm.Title.Trim(),
            Description = vm.Description,
            SubjectId = vm.SubjectId!.Value,
            SectionId = vm.SectionId!.Value,
            TeacherId = teacherId.Value,
            TermId = vm.TermId!.Value,
            AssignedDate = vm.AssignedDate,
            DueDate = vm.DueDate,
            MaxScore = vm.MaxScore,
            IsPublished = vm.IsPublished
        };

        try
        {
            homework.AttachmentPath = await _files.SaveAsync(vm.Attachment, "homework");
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(vm.Attachment), ex.Message);
            await FillListsAsync(vm);
            return View("Form", vm);
        }

        _db.Homeworks.Add(homework);
        await _db.SaveChangesAsync();

        // إنشاء سجلات تسليم لكل الطلاب
        var studentIds = await _db.Students
            .Where(s => s.CurrentSectionId == homework.SectionId && s.Status == StudentStatus.Active)
            .Select(s => s.Id).ToListAsync();

        _db.HomeworkSubmissions.AddRange(studentIds.Select(id => new HomeworkSubmission
        {
            HomeworkId = homework.Id,
            StudentId = id,
            Status = HomeworkStatus.NotSubmitted
        }));
        await _db.SaveChangesAsync();

        if (homework.IsPublished)
        {
            var userIds = await _db.Users
                .Where(u => u.StudentId != null && studentIds.Contains(u.StudentId.Value))
                .Select(u => u.Id).ToListAsync();

            if (userIds.Count > 0)
                await _notify.NotifyUsersAsync(userIds, "واجب جديد",
                    $"{homework.Title} — موعد التسليم {homework.DueDate:yyyy/MM/dd}",
                    NotificationType.Homework, NotificationSeverity.Info, "/Portal/Student/Homework");
        }

        Success($"تم إنشاء الواجب لـ {studentIds.Count} طالب.");
        return RedirectToAction(nameof(Grade), new { id = homework.Id });
    }

    [HasPermission(Permissions.HomeworkManage)]
    public async Task<IActionResult> Edit(int id)
    {
        var h = await _db.Homeworks.FindAsync(id);
        if (h is null) return NotFound();

        var vm = new HomeworkFormViewModel
        {
            Id = h.Id,
            Title = h.Title,
            Description = h.Description,
            SubjectId = h.SubjectId,
            SectionId = h.SectionId,
            TeacherId = h.TeacherId,
            TermId = h.TermId,
            AssignedDate = h.AssignedDate,
            DueDate = h.DueDate,
            MaxScore = h.MaxScore,
            AttachmentPath = h.AttachmentPath,
            IsPublished = h.IsPublished
        };

        await FillListsAsync(vm);
        return View("Form", vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.HomeworkManage)]
    public async Task<IActionResult> Edit(HomeworkFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await FillListsAsync(vm);
            return View("Form", vm);
        }

        var h = await _db.Homeworks.FindAsync(vm.Id);
        if (h is null) return NotFound();

        h.Title = vm.Title.Trim();
        h.Description = vm.Description;
        h.SubjectId = vm.SubjectId!.Value;
        h.TermId = vm.TermId!.Value;
        h.AssignedDate = vm.AssignedDate;
        h.DueDate = vm.DueDate;
        h.MaxScore = vm.MaxScore;
        h.IsPublished = vm.IsPublished;
        if (vm.TeacherId.HasValue) h.TeacherId = vm.TeacherId.Value;

        if (vm.Attachment is not null)
        {
            try
            {
                var path = await _files.SaveAsync(vm.Attachment, "homework");
                if (path is not null)
                {
                    _files.Delete(h.AttachmentPath);
                    h.AttachmentPath = path;
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(vm.Attachment), ex.Message);
                await FillListsAsync(vm);
                return View("Form", vm);
            }
        }

        await _db.SaveChangesAsync();
        Success("تم حفظ الواجب.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.HomeworkManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var h = await _db.Homeworks.FindAsync(id);
        if (h is null) return NotFound();

        _files.Delete(h.AttachmentPath);
        _db.Homeworks.Remove(h);
        await _db.SaveChangesAsync();

        Success("تم حذف الواجب.");
        return RedirectToAction(nameof(Index));
    }

    private async Task FillListsAsync(HomeworkFormViewModel vm)
    {
        var year = await GetCurrentYearAsync();
        var teacherId = await MyTeacherIdAsync();

        var sections = _db.Sections.AsNoTracking()
            .Where(s => s.IsActive && (year == null || s.AcademicYearId == year.Id));

        if (teacherId is not null)
        {
            var mine = await _db.TeacherSubjects
                .Where(ts => ts.TeacherId == teacherId && (year == null || ts.AcademicYearId == year.Id))
                .Select(ts => ts.SectionId).Distinct().ToListAsync();
            sections = sections.Where(s => mine.Contains(s.Id));
        }

        vm.Sections = await sections.OrderBy(s => s.Grade.SeqNo).ThenBy(s => s.Name)
            .Select(s => new SelectListItem(s.Grade.Name + " - " + s.Name, s.Id.ToString(), s.Id == vm.SectionId))
            .ToListAsync();

        vm.Subjects = await _db.Subjects.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString(), s.Id == vm.SubjectId)).ToListAsync();

        vm.Terms = await _db.Terms.AsNoTracking()
            .Where(t => year == null || t.AcademicYearId == year.Id).OrderBy(t => t.SeqNo)
            .Select(t => new SelectListItem(t.Name, t.Id.ToString(), t.Id == vm.TermId)).ToListAsync();

        vm.Teachers = await _db.Employees.AsNoTracking()
            .Where(e => e.EmployeeType == EmployeeType.Teacher && e.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new SelectListItem(e.FullName, e.Id.ToString(), e.Id == vm.TeacherId)).ToListAsync();
    }

    // ==================================================================
    // تصحيح الواجب
    // ==================================================================
    public async Task<IActionResult> Grade(int id)
    {
        var hw = await _db.Homeworks
            .Include(h => h.Subject)
            .Include(h => h.Section).ThenInclude(s => s.Grade)
            .FirstOrDefaultAsync(h => h.Id == id);

        if (hw is null) return NotFound();

        var teacherId = await MyTeacherIdAsync();
        if (teacherId is not null && hw.TeacherId != teacherId) return Forbid();

        var vm = new HomeworkGradeViewModel
        {
            HomeworkId = hw.Id,
            Title = hw.Title,
            Subject = hw.Subject.Name,
            Section = $"{hw.Section.Grade.Name} - {hw.Section.Name}",
            DueDate = hw.DueDate,
            MaxScore = hw.MaxScore,
            Description = hw.Description,
            AttachmentPath = hw.AttachmentPath
        };

        vm.Entries = await _db.HomeworkSubmissions.AsNoTracking()
            .Where(s => s.HomeworkId == id)
            .OrderBy(s => s.Student.FullName)
            .Select(s => new SubmissionEntry
            {
                SubmissionId = s.Id,
                StudentId = s.StudentId,
                StudentNo = s.Student.StudentNo,
                StudentName = s.Student.FullName,
                PhotoPath = s.Student.PhotoPath,
                Status = s.Status,
                SubmittedAt = s.SubmittedAt,
                FilePath = s.FilePath,
                AnswerText = s.AnswerText,
                Score = s.Score,
                Feedback = s.Feedback
            })
            .ToListAsync();

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.HomeworkGrade)]
    public async Task<IActionResult> SaveGrades(int homeworkId, List<SubmissionEntry> entries)
    {
        var hw = await _db.Homeworks.FindAsync(homeworkId);
        if (hw is null) return NotFound();

        var teacherId = await MyTeacherIdAsync();
        if (teacherId is not null && hw.TeacherId != teacherId) return Forbid();

        var subs = await _db.HomeworkSubmissions
            .Where(s => s.HomeworkId == homeworkId)
            .ToDictionaryAsync(s => s.StudentId);

        foreach (var entry in entries)
        {
            if (!subs.TryGetValue(entry.StudentId, out var sub)) continue;

            if (entry.Score.HasValue && (entry.Score < 0 || entry.Score > hw.MaxScore)) continue;

            sub.Score = entry.Score;
            sub.Feedback = entry.Feedback;

            if (entry.Score.HasValue)
            {
                sub.Status = HomeworkStatus.Graded;
                sub.GradedAt = DateTime.Now;
            }
        }

        await _db.SaveChangesAsync();
        Success("تم حفظ درجات الواجب.");
        return RedirectToAction(nameof(Grade), new { id = homeworkId });
    }
}
