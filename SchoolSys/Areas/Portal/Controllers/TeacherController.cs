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
[Authorize(Roles = RoleNames.Teacher + "," + RoleNames.SuperAdmin)]
public class TeacherController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;

    public TeacherController(ApplicationDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task<IActionResult> Index()
    {
        var employeeId = await _user.GetEmployeeIdAsync();
        var year = await GetCurrentYearAsync();
        var today = DateTime.Today;

        var vm = new TeacherPortalViewModel { TeacherName = User.DisplayName() };

        if (employeeId is null)
        {
            Warning("حسابك غير مرتبط بسجل موظف. يرجى مراجعة مسؤول النظام.");
            return View(vm);
        }

        var load = await _db.TeacherSubjects.AsNoTracking()
            .Where(ts => ts.TeacherId == employeeId && (year == null || ts.AcademicYearId == year.Id))
            .Select(ts => new
            {
                ts.SectionId,
                SectionName = ts.Section.Grade.Name + " - " + ts.Section.Name,
                SubjectName = ts.Subject.Name,
                Students = ts.Section.Enrollments.Count(e => e.IsActive)
            })
            .ToListAsync();

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

        vm.StudentsCount = vm.Sections.Sum(s => s.Students);

        var dayIndex = (int)today.DayOfWeek;
        vm.TodayLessons = await _db.TimetableSlots.AsNoTracking()
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
            .CountAsync(s => s.Homework.TeacherId == employeeId && s.Status == HomeworkStatus.Submitted);

        var sectionIds = vm.Sections.Select(s => s.SectionId).ToList();
        vm.PendingMarks = await _db.Exams
            .CountAsync(e => sectionIds.Contains(e.SectionId) && e.ExamDate < today &&
                             e.Status != ExamStatus.Graded && e.Status != ExamStatus.Approved);

        return View(vm);
    }
}
