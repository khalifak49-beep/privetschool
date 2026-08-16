using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[HasPermission(Permissions.TimetableView)]
public class TimetableController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;

    public TimetableController(ApplicationDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    /// <summary>أوقات الحصص الافتراضية.</summary>
    private static readonly PeriodTime[] DefaultPeriods =
    [
        new(1, new(7, 30, 0), new(8, 15, 0)),
        new(2, new(8, 15, 0), new(9, 0, 0)),
        new(3, new(9, 20, 0), new(10, 5, 0)),
        new(4, new(10, 5, 0), new(10, 50, 0)),
        new(5, new(11, 10, 0), new(11, 55, 0)),
        new(6, new(11, 55, 0), new(12, 40, 0)),
        new(7, new(12, 50, 0), new(13, 30, 0))
    ];

    public async Task<IActionResult> Index(int? sectionId, int? teacherId, string mode = "section")
    {
        var year = await GetCurrentYearAsync();

        // المعلم يرى جدوله افتراضياً
        if (User.IsInRole(RoleNames.Teacher) && !User.Can(Permissions.TimetableManage)
            && teacherId is null && sectionId is null)
        {
            teacherId = await _user.GetEmployeeIdAsync();
            mode = "teacher";
        }

        var vm = new TimetableViewModel
        {
            SectionId = sectionId,
            TeacherId = teacherId,
            Mode = mode,
            PeriodTimes = DefaultPeriods.ToList()
        };

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

        // اختيار افتراضي: أول شعبة
        if (mode == "section" && sectionId is null && vm.Sections.Count > 0)
        {
            sectionId = int.Parse(vm.Sections[0].Value);
            vm.SectionId = sectionId;
            vm.Sections[0].Selected = true;
        }

        var query = _db.TimetableSlots.AsNoTracking()
            .Where(t => year == null || t.AcademicYearId == year.Id);

        if (mode == "teacher" && teacherId.HasValue)
        {
            query = query.Where(t => t.TeacherId == teacherId);
            vm.Title = await _db.Employees.Where(e => e.Id == teacherId).Select(e => e.FullName).FirstOrDefaultAsync();
        }
        else if (sectionId.HasValue)
        {
            query = query.Where(t => t.SectionId == sectionId);
            vm.Title = await _db.Sections.Where(s => s.Id == sectionId)
                .Select(s => s.Grade.Name + " - " + s.Name).FirstOrDefaultAsync();
        }
        else
        {
            query = query.Where(t => false);
        }

        var slots = await query
            .Select(t => new
            {
                t.Id,
                t.DayOfWeek,
                t.PeriodNo,
                Subject = t.Subject.Name,
                Teacher = t.Teacher.FullName,
                Section = t.Section.Grade.Name + " - " + t.Section.Name,
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
                Section = s.Section,
                Room = s.Room,
                StartTime = s.StartTime,
                EndTime = s.EndTime
            };
        }

        vm.MaxPeriods = Math.Max(6, slots.Count > 0 ? slots.Max(s => s.PeriodNo) : 6);

        // خيارات الإضافة تعتمد على النصاب المسند للشعبة
        if (sectionId.HasValue)
        {
            var load = await _db.TeacherSubjects.AsNoTracking()
                .Where(ts => ts.SectionId == sectionId && (year == null || ts.AcademicYearId == year.Id))
                .Select(ts => new { ts.SubjectId, SubjectName = ts.Subject.Name, ts.TeacherId, TeacherName = ts.Teacher.FullName })
                .ToListAsync();

            vm.SubjectOptions = load
                .GroupBy(l => new { l.SubjectId, l.SubjectName })
                .Select(g => new SelectListItem(g.Key.SubjectName, g.Key.SubjectId.ToString()))
                .ToList();

            vm.TeacherOptions = load
                .GroupBy(l => new { l.TeacherId, l.TeacherName })
                .Select(g => new SelectListItem(g.Key.TeacherName, g.Key.TeacherId.ToString()))
                .ToList();

            ViewBag.SubjectTeacherMap = load
                .GroupBy(l => l.SubjectId)
                .ToDictionary(g => g.Key.ToString(),
                    g => g.Select(x => new { id = x.TeacherId, name = x.TeacherName }).Distinct().ToList());
        }

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.TimetableManage)]
    public async Task<IActionResult> SaveSlot(int id, int sectionId, int subjectId, int teacherId,
        int dayOfWeek, int periodNo, string? room)
    {
        var year = await GetCurrentYearAsync();
        if (year is null)
        {
            Error("لا يوجد عام دراسي محدد.");
            return RedirectToAction(nameof(Index), new { sectionId });
        }

        var period = DefaultPeriods.FirstOrDefault(p => p.PeriodNo == periodNo) ?? DefaultPeriods[0];

        // تعارض المعلم: نفس اليوم ونفس الحصة في شعبة أخرى
        var conflict = await _db.TimetableSlots.AnyAsync(t =>
            t.Id != id && t.AcademicYearId == year.Id &&
            t.TeacherId == teacherId && t.DayOfWeek == dayOfWeek && t.PeriodNo == periodNo);

        if (conflict)
        {
            Error("تعارض في الجدول: المعلم لديه حصة أخرى في نفس اليوم والحصة.");
            return RedirectToAction(nameof(Index), new { sectionId });
        }

        if (id > 0)
        {
            var slot = await _db.TimetableSlots.FindAsync(id);
            if (slot is null) return NotFound();

            slot.SubjectId = subjectId;
            slot.TeacherId = teacherId;
            slot.Room = room;
            slot.DayOfWeek = dayOfWeek;
            slot.PeriodNo = periodNo;
            slot.StartTime = period.Start;
            slot.EndTime = period.End;
        }
        else
        {
            var occupied = await _db.TimetableSlots.AnyAsync(t =>
                t.SectionId == sectionId && t.DayOfWeek == dayOfWeek &&
                t.PeriodNo == periodNo && t.AcademicYearId == year.Id);

            if (occupied)
            {
                Error("هذه الحصة مشغولة بالفعل لهذه الشعبة.");
                return RedirectToAction(nameof(Index), new { sectionId });
            }

            _db.TimetableSlots.Add(new TimetableSlot
            {
                SectionId = sectionId,
                SubjectId = subjectId,
                TeacherId = teacherId,
                AcademicYearId = year.Id,
                DayOfWeek = dayOfWeek,
                PeriodNo = periodNo,
                StartTime = period.Start,
                EndTime = period.End,
                Room = room
            });
        }

        await _db.SaveChangesAsync();
        Success("تم حفظ الحصة في الجدول.");
        return RedirectToAction(nameof(Index), new { sectionId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.TimetableManage)]
    public async Task<IActionResult> DeleteSlot(int id, int? sectionId)
    {
        var slot = await _db.TimetableSlots.FindAsync(id);
        if (slot is not null)
        {
            _db.TimetableSlots.Remove(slot);
            await _db.SaveChangesAsync();
            Success("تم حذف الحصة.");
        }
        return RedirectToAction(nameof(Index), new { sectionId = sectionId ?? slot?.SectionId });
    }
}
