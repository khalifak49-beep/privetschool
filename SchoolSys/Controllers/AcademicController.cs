using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[HasPermission(Permissions.AcademicView)]
public class AcademicController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public AcademicController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    // ==================================================================
    // الصفحة الرئيسية للبنية الأكاديمية (تبويبات)
    // ==================================================================
    public async Task<IActionResult> Index(string tab = "years")
    {
        var year = await GetCurrentYearAsync();
        var vm = new AcademicIndexViewModel
        {
            ActiveTab = tab,
            CurrentYearId = year?.Id,
            CurrentYearName = year?.Name
        };

        vm.Years = await _db.AcademicYears.AsNoTracking()
            .OrderByDescending(y => y.StartDate).ToListAsync();

        vm.Terms = await _db.Terms.AsNoTracking()
            .Include(t => t.AcademicYear)
            .OrderByDescending(t => t.AcademicYear.StartDate).ThenBy(t => t.SeqNo)
            .ToListAsync();

        vm.Stages = await _db.Stages.AsNoTracking()
            .OrderBy(s => s.SeqNo)
            .Select(s => new StageRow
            {
                Id = s.Id,
                Name = s.Name,
                SeqNo = s.SeqNo,
                IsActive = s.IsActive,
                GradesCount = s.Grades.Count,
                StudentsCount = _db.Students.Count(st => st.Status == StudentStatus.Active &&
                                                         st.CurrentSection!.Grade.StageId == s.Id)
            })
            .ToListAsync();

        vm.Grades = await _db.Grades.AsNoTracking()
            .OrderBy(g => g.SeqNo)
            .Select(g => new GradeRow
            {
                Id = g.Id,
                Name = g.Name,
                SeqNo = g.SeqNo,
                StageId = g.StageId,
                StageName = g.Stage.Name,
                IsActive = g.IsActive,
                SectionsCount = g.Sections.Count(s => year == null || s.AcademicYearId == year.Id),
                StudentsCount = _db.Students.Count(st => st.Status == StudentStatus.Active &&
                                                         st.CurrentSection!.GradeId == g.Id)
            })
            .ToListAsync();

        var sectionQuery = _db.Sections.AsNoTracking().AsQueryable();
        if (year is not null) sectionQuery = sectionQuery.Where(s => s.AcademicYearId == year.Id);

        vm.Sections = await sectionQuery
            .OrderBy(s => s.Grade.SeqNo).ThenBy(s => s.Name)
            .Select(s => new SectionRow
            {
                Id = s.Id,
                Name = s.Name,
                GradeId = s.GradeId,
                GradeName = s.Grade.Name,
                StageName = s.Grade.Stage.Name,
                Capacity = s.Capacity,
                Room = s.Room,
                HomeroomTeacherId = s.HomeroomTeacherId,
                HomeroomTeacher = s.HomeroomTeacher != null ? s.HomeroomTeacher.FullName : null,
                StudentsCount = s.Enrollments.Count(e => e.IsActive),
                IsActive = s.IsActive
            })
            .ToListAsync();

        vm.Subjects = await _db.Subjects.AsNoTracking()
            .OrderBy(s => s.Code)
            .Select(s => new SubjectRow
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                StageId = s.StageId,
                StageName = s.Stage != null ? s.Stage.Name : null,
                MaxScore = s.MaxScore,
                PassScore = s.PassScore,
                WeeklyPeriods = s.WeeklyPeriods,
                IsActive = s.IsActive,
                TeachersCount = _db.TeacherSubjects
                    .Where(ts => ts.SubjectId == s.Id && (year == null || ts.AcademicYearId == year.Id))
                    .Select(ts => ts.TeacherId).Distinct().Count()
            })
            .ToListAsync();

        await FillOptionsAsync(vm);
        return View(vm);
    }

    private async Task FillOptionsAsync(AcademicIndexViewModel vm)
    {
        vm.StageOptions = await _db.Stages.AsNoTracking().OrderBy(s => s.SeqNo)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToListAsync();

        vm.GradeOptions = await _db.Grades.AsNoTracking().OrderBy(g => g.SeqNo)
            .Select(g => new SelectListItem(g.Stage.Name + " / " + g.Name, g.Id.ToString())).ToListAsync();

        vm.YearOptions = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate)
            .Select(y => new SelectListItem(y.Name, y.Id.ToString())).ToListAsync();

        vm.TeacherOptions = await _db.Employees.AsNoTracking()
            .Where(e => e.EmployeeType == EmployeeType.Teacher && e.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new SelectListItem(e.FullName, e.Id.ToString())).ToListAsync();
    }

    // ==================================================================
    // الأعوام الدراسية
    // ==================================================================
    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AcademicManage)]
    public async Task<IActionResult> SaveYear(AcademicYear model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            Error("الرجاء إدخال اسم العام الدراسي.");
            return RedirectToAction(nameof(Index), new { tab = "years" });
        }

        if (model.EndDate <= model.StartDate)
        {
            Error("تاريخ نهاية العام يجب أن يكون بعد تاريخ البداية.");
            return RedirectToAction(nameof(Index), new { tab = "years" });
        }

        if (model.Id == 0)
        {
            if (await _db.AcademicYears.AnyAsync(y => y.Name == model.Name))
            {
                Error("يوجد عام دراسي بنفس الاسم.");
                return RedirectToAction(nameof(Index), new { tab = "years" });
            }
            _db.AcademicYears.Add(model);
        }
        else
        {
            var y = await _db.AcademicYears.FindAsync(model.Id);
            if (y is null) return NotFound();
            y.Name = model.Name;
            y.StartDate = model.StartDate;
            y.EndDate = model.EndDate;
            y.IsClosed = model.IsClosed;
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("حفظ عام دراسي", nameof(AcademicYear), model.Id, model.Name);

        Success("تم حفظ العام الدراسي.");
        return RedirectToAction(nameof(Index), new { tab = "years" });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AcademicManage)]
    public async Task<IActionResult> SetCurrentYear(int id)
    {
        var years = await _db.AcademicYears.ToListAsync();
        foreach (var y in years) y.IsCurrent = y.Id == id;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("تغيير العام الدراسي الحالي", nameof(AcademicYear), id);

        Success("تم تعيين العام الدراسي الحالي.");
        return RedirectToAction(nameof(Index), new { tab = "years" });
    }

    // ==================================================================
    // الفصول الدراسية
    // ==================================================================
    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AcademicManage)]
    public async Task<IActionResult> SaveTerm(Term model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            Error("الرجاء إدخال اسم الفصل الدراسي.");
            return RedirectToAction(nameof(Index), new { tab = "terms" });
        }

        if (model.Id == 0) _db.Terms.Add(model);
        else
        {
            var t = await _db.Terms.FindAsync(model.Id);
            if (t is null) return NotFound();
            t.Name = model.Name;
            t.SeqNo = model.SeqNo;
            t.AcademicYearId = model.AcademicYearId;
            t.StartDate = model.StartDate;
            t.EndDate = model.EndDate;
        }

        await _db.SaveChangesAsync();
        Success("تم حفظ الفصل الدراسي.");
        return RedirectToAction(nameof(Index), new { tab = "terms" });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AcademicManage)]
    public async Task<IActionResult> SetCurrentTerm(int id)
    {
        var terms = await _db.Terms.ToListAsync();
        foreach (var t in terms) t.IsCurrent = t.Id == id;

        await _db.SaveChangesAsync();
        Success("تم تعيين الفصل الدراسي الحالي.");
        return RedirectToAction(nameof(Index), new { tab = "terms" });
    }

    // ==================================================================
    // المراحل
    // ==================================================================
    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AcademicManage)]
    public async Task<IActionResult> SaveStage(Stage model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            Error("الرجاء إدخال اسم المرحلة.");
            return RedirectToAction(nameof(Index), new { tab = "stages" });
        }

        if (model.Id == 0) _db.Stages.Add(model);
        else
        {
            var s = await _db.Stages.FindAsync(model.Id);
            if (s is null) return NotFound();
            s.Name = model.Name;
            s.SeqNo = model.SeqNo;
            s.IsActive = model.IsActive;
        }

        await _db.SaveChangesAsync();
        Success("تم حفظ المرحلة الدراسية.");
        return RedirectToAction(nameof(Index), new { tab = "stages" });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AcademicManage)]
    public async Task<IActionResult> DeleteStage(int id)
    {
        if (await _db.Grades.AnyAsync(g => g.StageId == id))
        {
            Error("لا يمكن حذف المرحلة لاحتوائها على صفوف دراسية.");
            return RedirectToAction(nameof(Index), new { tab = "stages" });
        }

        var s = await _db.Stages.FindAsync(id);
        if (s is not null)
        {
            _db.Stages.Remove(s);
            await _db.SaveChangesAsync();
            Success("تم حذف المرحلة.");
        }
        return RedirectToAction(nameof(Index), new { tab = "stages" });
    }

    // ==================================================================
    // الصفوف
    // ==================================================================
    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AcademicManage)]
    public async Task<IActionResult> SaveGrade(Grade model)
    {
        if (string.IsNullOrWhiteSpace(model.Name) || model.StageId == 0)
        {
            Error("الرجاء إدخال اسم الصف واختيار المرحلة.");
            return RedirectToAction(nameof(Index), new { tab = "grades" });
        }

        if (model.Id == 0) _db.Grades.Add(model);
        else
        {
            var g = await _db.Grades.FindAsync(model.Id);
            if (g is null) return NotFound();
            g.Name = model.Name;
            g.SeqNo = model.SeqNo;
            g.StageId = model.StageId;
            g.IsActive = model.IsActive;
        }

        await _db.SaveChangesAsync();
        Success("تم حفظ الصف الدراسي.");
        return RedirectToAction(nameof(Index), new { tab = "grades" });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AcademicManage)]
    public async Task<IActionResult> DeleteGrade(int id)
    {
        if (await _db.Sections.AnyAsync(s => s.GradeId == id))
        {
            Error("لا يمكن حذف الصف لاحتوائه على شعب.");
            return RedirectToAction(nameof(Index), new { tab = "grades" });
        }

        var g = await _db.Grades.FindAsync(id);
        if (g is not null)
        {
            _db.Grades.Remove(g);
            await _db.SaveChangesAsync();
            Success("تم حذف الصف.");
        }
        return RedirectToAction(nameof(Index), new { tab = "grades" });
    }

    // ==================================================================
    // الشعب
    // ==================================================================
    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AcademicManage)]
    public async Task<IActionResult> SaveSection(Section model)
    {
        if (string.IsNullOrWhiteSpace(model.Name) || model.GradeId == 0)
        {
            Error("الرجاء إدخال اسم الشعبة واختيار الصف.");
            return RedirectToAction(nameof(Index), new { tab = "sections" });
        }

        if (model.AcademicYearId == 0)
        {
            var year = await GetCurrentYearAsync();
            if (year is null)
            {
                Error("لا يوجد عام دراسي محدد. أضف عاماً دراسياً أولاً.");
                return RedirectToAction(nameof(Index), new { tab = "years" });
            }
            model.AcademicYearId = year.Id;
        }

        if (model.Id == 0) _db.Sections.Add(model);
        else
        {
            var s = await _db.Sections.FindAsync(model.Id);
            if (s is null) return NotFound();
            s.Name = model.Name;
            s.GradeId = model.GradeId;
            s.Capacity = model.Capacity;
            s.Room = model.Room;
            s.HomeroomTeacherId = model.HomeroomTeacherId == 0 ? null : model.HomeroomTeacherId;
            s.IsActive = model.IsActive;
        }

        await _db.SaveChangesAsync();
        Success("تم حفظ الشعبة.");
        return RedirectToAction(nameof(Index), new { tab = "sections" });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AcademicManage)]
    public async Task<IActionResult> DeleteSection(int id)
    {
        if (await _db.Students.AnyAsync(s => s.CurrentSectionId == id) ||
            await _db.Enrollments.AnyAsync(e => e.SectionId == id))
        {
            Error("لا يمكن حذف الشعبة لوجود طلاب مسجّلين بها.");
            return RedirectToAction(nameof(Index), new { tab = "sections" });
        }

        var s = await _db.Sections.FindAsync(id);
        if (s is not null)
        {
            try
            {
                _db.Sections.Remove(s);
                await _db.SaveChangesAsync();
                Success("تم حذف الشعبة.");
            }
            catch (DbUpdateException)
            {
                Error("تعذّر حذف الشعبة لارتباطها بجداول أو اختبارات.");
            }
        }
        return RedirectToAction(nameof(Index), new { tab = "sections" });
    }

    // ==================================================================
    // المواد
    // ==================================================================
    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AcademicManage)]
    public async Task<IActionResult> SaveSubject(Subject model)
    {
        if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Code))
        {
            Error("الرجاء إدخال رمز المادة واسمها.");
            return RedirectToAction(nameof(Index), new { tab = "subjects" });
        }

        model.Code = model.Code.Trim().ToUpperInvariant();

        if (model.Id == 0)
        {
            if (await _db.Subjects.AnyAsync(s => s.Code == model.Code))
            {
                Error("رمز المادة مستخدم مسبقاً.");
                return RedirectToAction(nameof(Index), new { tab = "subjects" });
            }
            if (model.StageId == 0) model.StageId = null;
            _db.Subjects.Add(model);
        }
        else
        {
            var s = await _db.Subjects.FindAsync(model.Id);
            if (s is null) return NotFound();
            s.Code = model.Code;
            s.Name = model.Name;
            s.StageId = model.StageId == 0 ? null : model.StageId;
            s.MaxScore = model.MaxScore;
            s.PassScore = model.PassScore;
            s.WeeklyPeriods = model.WeeklyPeriods;
            s.IsActive = model.IsActive;
        }

        await _db.SaveChangesAsync();
        Success("تم حفظ المادة الدراسية.");
        return RedirectToAction(nameof(Index), new { tab = "subjects" });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AcademicManage)]
    public async Task<IActionResult> DeleteSubject(int id)
    {
        if (await _db.TeacherSubjects.AnyAsync(ts => ts.SubjectId == id) ||
            await _db.Exams.AnyAsync(e => e.SubjectId == id))
        {
            Error("لا يمكن حذف المادة لارتباطها بإسنادات أو اختبارات.");
            return RedirectToAction(nameof(Index), new { tab = "subjects" });
        }

        var s = await _db.Subjects.FindAsync(id);
        if (s is not null)
        {
            _db.Subjects.Remove(s);
            await _db.SaveChangesAsync();
            Success("تم حذف المادة.");
        }
        return RedirectToAction(nameof(Index), new { tab = "subjects" });
    }

    // ==================================================================
    // توزيع المواد على المعلمين
    // ==================================================================
    public async Task<IActionResult> TeachingLoad(int? sectionId, int? teacherId, int? subjectId)
    {
        var year = await GetCurrentYearAsync();
        var vm = new TeachingLoadViewModel
        {
            SectionId = sectionId,
            TeacherId = teacherId,
            SubjectId = subjectId,
            CurrentYearName = year?.Name
        };

        var query = _db.TeacherSubjects.AsNoTracking()
            .Where(ts => year == null || ts.AcademicYearId == year.Id);

        if (sectionId.HasValue) query = query.Where(ts => ts.SectionId == sectionId);
        if (teacherId.HasValue) query = query.Where(ts => ts.TeacherId == teacherId);
        if (subjectId.HasValue) query = query.Where(ts => ts.SubjectId == subjectId);

        vm.Assignments = await query
            .OrderBy(ts => ts.Section.Grade.SeqNo).ThenBy(ts => ts.Section.Name).ThenBy(ts => ts.Subject.Name)
            .Select(ts => new TeachingAssignmentRow
            {
                Id = ts.Id,
                TeacherId = ts.TeacherId,
                TeacherName = ts.Teacher.FullName,
                SubjectId = ts.SubjectId,
                SubjectName = ts.Subject.Name,
                SectionId = ts.SectionId,
                SectionName = ts.Section.Grade.Name + " - " + ts.Section.Name,
                Students = ts.Section.Enrollments.Count(e => e.IsActive),
                IsActive = ts.IsActive
            })
            .Take(600)
            .ToListAsync();

        vm.TotalAssignments = await _db.TeacherSubjects
            .CountAsync(ts => year == null || ts.AcademicYearId == year.Id);

        var sectionsWithLoad = await _db.TeacherSubjects
            .Where(ts => year == null || ts.AcademicYearId == year.Id)
            .Select(ts => ts.SectionId).Distinct().CountAsync();

        var totalSections = await _db.Sections
            .CountAsync(s => s.IsActive && (year == null || s.AcademicYearId == year.Id));

        vm.UnassignedSections = totalSections - sectionsWithLoad;

        vm.Sections = await _db.Sections.AsNoTracking()
            .Where(s => s.IsActive && (year == null || s.AcademicYearId == year.Id))
            .OrderBy(s => s.Grade.SeqNo).ThenBy(s => s.Name)
            .Select(s => new SelectListItem(s.Grade.Name + " - " + s.Name, s.Id.ToString(), s.Id == sectionId))
            .ToListAsync();

        vm.Teachers = await _db.Employees.AsNoTracking()
            .Where(e => e.EmployeeType == EmployeeType.Teacher && e.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new SelectListItem(e.FullName, e.Id.ToString(), e.Id == teacherId))
            .ToListAsync();

        vm.Subjects = await _db.Subjects.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString(), s.Id == subjectId))
            .ToListAsync();

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AcademicAssignSubjects)]
    public async Task<IActionResult> AssignSubject(int teacherId, int subjectId, int sectionId)
    {
        var year = await GetCurrentYearAsync();
        if (year is null)
        {
            Error("لا يوجد عام دراسي محدد.");
            return RedirectToAction(nameof(TeachingLoad));
        }

        var exists = await _db.TeacherSubjects.AnyAsync(ts =>
            ts.TeacherId == teacherId && ts.SubjectId == subjectId &&
            ts.SectionId == sectionId && ts.AcademicYearId == year.Id);

        if (exists)
        {
            Warning("هذا الإسناد موجود مسبقاً.");
            return RedirectToAction(nameof(TeachingLoad), new { sectionId });
        }

        _db.TeacherSubjects.Add(new TeacherSubject
        {
            TeacherId = teacherId,
            SubjectId = subjectId,
            SectionId = sectionId,
            AcademicYearId = year.Id
        });

        await _db.SaveChangesAsync();
        await _audit.LogAsync("إسناد مادة لمعلم", nameof(TeacherSubject), null,
            $"معلم:{teacherId} مادة:{subjectId} شعبة:{sectionId}");

        Success("تم إسناد المادة للمعلم.");
        return RedirectToAction(nameof(TeachingLoad), new { sectionId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AcademicAssignSubjects)]
    public async Task<IActionResult> RemoveAssignment(int id, int? sectionId)
    {
        var ts = await _db.TeacherSubjects.FindAsync(id);
        if (ts is null) return NotFound();

        var hasTimetable = await _db.TimetableSlots.AnyAsync(t =>
            t.TeacherId == ts.TeacherId && t.SubjectId == ts.SubjectId && t.SectionId == ts.SectionId);

        if (hasTimetable)
        {
            Error("لا يمكن إلغاء الإسناد لوجود حصص مجدولة. احذف الحصص من الجدول أولاً.");
            return RedirectToAction(nameof(TeachingLoad), new { sectionId });
        }

        _db.TeacherSubjects.Remove(ts);
        await _db.SaveChangesAsync();

        Success("تم إلغاء الإسناد.");
        return RedirectToAction(nameof(TeachingLoad), new { sectionId });
    }
}
