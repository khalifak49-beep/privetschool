using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[HasPermission(Permissions.ResultsView)]
public class ResultsController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IExportService _export;
    private readonly ICurrentUserService _user;

    public ResultsController(ApplicationDbContext db, IExportService export, ICurrentUserService user)
    {
        _db = db;
        _export = export;
        _user = user;
    }

    // ==================================================================
    // نتائج الشعبة مع الترتيب
    // ==================================================================
    public async Task<IActionResult> Index(int? sectionId, int? termId)
    {
        var year = await GetCurrentYearAsync();
        var term = termId.HasValue
            ? await _db.Terms.FindAsync(termId.Value)
            : await GetCurrentTermAsync();

        var vm = new ResultsIndexViewModel
        {
            SectionId = sectionId,
            TermId = term?.Id,
            TermName = term?.Name
        };

        vm.Sections = await _db.Sections.AsNoTracking()
            .Where(s => s.IsActive && (year == null || s.AcademicYearId == year.Id))
            .OrderBy(s => s.Grade.SeqNo).ThenBy(s => s.Name)
            .Select(s => new SelectListItem(s.Grade.Name + " - " + s.Name, s.Id.ToString(), s.Id == sectionId))
            .ToListAsync();

        vm.Terms = await _db.Terms.AsNoTracking()
            .Where(t => year == null || t.AcademicYearId == year.Id)
            .OrderBy(t => t.SeqNo)
            .Select(t => new SelectListItem(t.Name, t.Id.ToString(), t.Id == term!.Id))
            .ToListAsync();

        vm.Scales = await _db.GradeScales.AsNoTracking().OrderByDescending(g => g.MinPercent).ToListAsync();

        if (sectionId is null || term is null) return View(vm);

        vm.SectionName = await _db.Sections.Where(s => s.Id == sectionId)
            .Select(s => s.Grade.Name + " - " + s.Name).FirstOrDefaultAsync();

        vm.Rows = await BuildResultsAsync(sectionId.Value, term.Id, vm.Scales, vm.Subjects);
        return View(vm);
    }

    /// <summary>يحسب مجاميع الطلاب لكل مادة مع الترتيب والتقدير.</summary>
    private async Task<List<StudentResultRow>> BuildResultsAsync(
        int sectionId, int termId, List<GradeScale> scales, List<string> subjectNames)
    {
        var raw = await _db.ExamResults.AsNoTracking()
            .Where(r => r.Exam.SectionId == sectionId && r.Exam.TermId == termId &&
                        (r.Exam.Status == ExamStatus.Graded || r.Exam.Status == ExamStatus.Approved))
            .Select(r => new
            {
                r.StudentId,
                StudentName = r.Student.FullName,
                r.Student.StudentNo,
                Subject = r.Exam.Subject.Name,
                r.Score,
                r.IsAbsent,
                r.Exam.MaxScore
            })
            .ToListAsync();

        subjectNames.Clear();
        subjectNames.AddRange(raw.Select(r => r.Subject).Distinct().OrderBy(s => s));

        var rows = raw
            .GroupBy(r => new { r.StudentId, r.StudentName, r.StudentNo })
            .Select(g =>
            {
                var row = new StudentResultRow
                {
                    StudentId = g.Key.StudentId,
                    StudentName = g.Key.StudentName,
                    StudentNo = g.Key.StudentNo
                };

                foreach (var subject in g.GroupBy(x => x.Subject))
                {
                    var score = subject.Sum(x => x.Score ?? 0m);
                    var max = subject.Sum(x => x.MaxScore);
                    row.SubjectScores[subject.Key] = score;
                    row.SubjectMax[subject.Key] = max;
                    row.Total += score;
                    row.MaxTotal += max;
                }

                return row;
            })
            .OrderByDescending(r => r.Percentage)
            .ToList();

        // الترتيب مع معالجة التساوي
        var rank = 0;
        decimal? previous = null;
        for (var i = 0; i < rows.Count; i++)
        {
            if (previous is null || rows[i].Percentage != previous)
                rank = i + 1;

            rows[i].Rank = rank;
            previous = rows[i].Percentage;

            var scale = scales.FirstOrDefault(s => rows[i].Percentage >= s.MinPercent && rows[i].Percentage <= s.MaxPercent);
            rows[i].GradeLetter = scale?.Letter;
            rows[i].GradeName = scale?.Name;
            rows[i].GradeColor = scale?.Color;
            rows[i].IsPass = scale?.IsPass ?? rows[i].Percentage >= 50;
        }

        return rows;
    }

    // ==================================================================
    // كشف درجات الطالب
    // ==================================================================
    public async Task<IActionResult> ReportCard(int studentId, int? termId)
    {
        var student = await _db.Students
            .Include(s => s.CurrentSection).ThenInclude(sec => sec!.Grade).ThenInclude(g => g.Stage)
            .Include(s => s.CurrentSection).ThenInclude(sec => sec!.HomeroomTeacher)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        if (student is null) return NotFound();

        // الطالب وولي الأمر يريان بياناتهما فقط
        if (!await CanViewStudentAsync(studentId)) return Forbid();

        var year = await GetCurrentYearAsync();
        var term = termId.HasValue ? await _db.Terms.FindAsync(termId.Value) : await GetCurrentTermAsync();
        var settings = await GetSettingsAsync();
        var scales = await _db.GradeScales.AsNoTracking().OrderByDescending(g => g.MinPercent).ToListAsync();

        var vm = new ReportCardViewModel
        {
            Student = student,
            SectionName = student.CurrentSection is null ? null
                : $"{student.CurrentSection.Grade.Name} - {student.CurrentSection.Name}",
            StageName = student.CurrentSection?.Grade.Stage.Name,
            HomeroomTeacher = student.CurrentSection?.HomeroomTeacher?.FullName,
            TermName = term?.Name ?? "",
            YearName = year?.Name ?? "",
            Settings = settings,
            TermId = term?.Id
        };

        vm.Terms = await _db.Terms.AsNoTracking()
            .Where(t => year == null || t.AcademicYearId == year.Id)
            .OrderBy(t => t.SeqNo)
            .Select(t => new SelectListItem(t.Name, t.Id.ToString(), t.Id == term!.Id))
            .ToListAsync();

        if (term is not null)
        {
            var results = await _db.ExamResults.AsNoTracking()
                .Where(r => r.StudentId == studentId && r.Exam.TermId == term.Id &&
                            (r.Exam.Status == ExamStatus.Graded || r.Exam.Status == ExamStatus.Approved))
                .Select(r => new
                {
                    Subject = r.Exam.Subject.Name,
                    ExamTitle = r.Exam.Title,
                    r.Score,
                    r.IsAbsent,
                    r.Exam.MaxScore
                })
                .ToListAsync();

            vm.Subjects = results
                .GroupBy(r => r.Subject)
                .Select(g =>
                {
                    var score = g.Sum(x => x.Score ?? 0m);
                    var max = g.Sum(x => x.MaxScore);
                    var pct = max > 0 ? score / max * 100m : 0;
                    var scale = scales.FirstOrDefault(s => pct >= s.MinPercent && pct <= s.MaxPercent);

                    return new ReportCardSubject
                    {
                        Subject = g.Key,
                        Score = score,
                        MaxScore = max,
                        Letter = scale?.Letter,
                        IsPass = scale?.IsPass ?? pct >= 50,
                        Exams = g.Select(x => (x.ExamTitle, x.Score, x.MaxScore)).ToList()
                    };
                })
                .OrderBy(s => s.Subject)
                .ToList();

            vm.Total = vm.Subjects.Sum(s => s.Score);
            vm.MaxTotal = vm.Subjects.Sum(s => s.MaxScore);

            var overall = scales.FirstOrDefault(s => vm.Percentage >= s.MinPercent && vm.Percentage <= s.MaxPercent);
            vm.GradeLetter = overall?.Letter;
            vm.GradeName = overall?.Name;
            vm.IsPass = overall?.IsPass ?? vm.Percentage >= 50;

            // الترتيب داخل الشعبة
            if (student.CurrentSectionId.HasValue)
            {
                var classRows = await BuildResultsAsync(student.CurrentSectionId.Value, term.Id, scales, []);
                vm.ClassSize = classRows.Count;
                vm.Rank = classRows.FirstOrDefault(r => r.StudentId == studentId)?.Rank ?? 0;
            }

            // الحضور خلال الفصل
            var att = await _db.StudentAttendances.AsNoTracking()
                .Where(a => a.StudentId == studentId && a.Date >= term.StartDate && a.Date <= term.EndDate)
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            vm.Attendance = new AttendanceSummary
            {
                Present = att.FirstOrDefault(a => a.Status == AttendanceStatus.Present)?.Count ?? 0,
                Absent = att.FirstOrDefault(a => a.Status == AttendanceStatus.Absent)?.Count ?? 0,
                Late = att.FirstOrDefault(a => a.Status == AttendanceStatus.Late)?.Count ?? 0,
                Excused = att.FirstOrDefault(a => a.Status == AttendanceStatus.Excused)?.Count ?? 0
            };
        }

        return View(vm);
    }

    /// <summary>الشهادة المطبوعة.</summary>
    [HasPermission(Permissions.ResultsCertificates)]
    public async Task<IActionResult> Certificate(int studentId, int? termId)
    {
        var result = await ReportCard(studentId, termId);
        if (result is not ViewResult view) return result;

        return View("Certificate", view.Model);
    }

    private async Task<bool> CanViewStudentAsync(int studentId)
    {
        if (User.Can(Permissions.StudentsView)) return true;

        if (User.IsInRole(RoleNames.Student))
            return await _user.GetStudentIdAsync() == studentId;

        if (User.IsInRole(RoleNames.Guardian))
        {
            var guardianId = await _user.GetGuardianIdAsync();
            return guardianId is not null &&
                   await _db.StudentGuardians.AnyAsync(sg => sg.GuardianId == guardianId && sg.StudentId == studentId);
        }

        return false;
    }

    // ==================================================================
    [HasPermission(Permissions.ReportsExport)]
    public async Task<IActionResult> Export(int sectionId, int termId, string format = "excel")
    {
        var scales = await _db.GradeScales.AsNoTracking().OrderByDescending(g => g.MinPercent).ToListAsync();
        var subjects = new List<string>();
        var rows = await BuildResultsAsync(sectionId, termId, scales, subjects);

        var settings = await GetSettingsAsync();
        var sectionName = await _db.Sections.Where(s => s.Id == sectionId)
            .Select(s => s.Grade.Name + " - " + s.Name).FirstOrDefaultAsync();
        var termName = await _db.Terms.Where(t => t.Id == termId).Select(t => t.Name).FirstOrDefaultAsync();

        var columns = new List<ExportColumn> { new("الترتيب", .7f), new("الرقم", 1f), new("اسم الطالب", 2.2f) };
        columns.AddRange(subjects.Select(s => new ExportColumn(s, 1f)));
        columns.Add(new ExportColumn("المجموع", 1f));
        columns.Add(new ExportColumn("النسبة %", .9f));
        columns.Add(new ExportColumn("التقدير", .9f));

        var data = rows.Select(r =>
        {
            var cells = new List<string?> { r.Rank.ToString(), r.StudentNo, r.StudentName };
            cells.AddRange(subjects.Select(s =>
                r.SubjectScores.TryGetValue(s, out var v) && v.HasValue ? v.Value.ToString("0.##") : "—"));
            cells.Add($"{r.Total:0.##} / {r.MaxTotal:0.##}");
            cells.Add(r.Percentage.ToString("0.##"));
            cells.Add(r.GradeLetter ?? "—");
            return cells.ToArray();
        });

        var title = $"كشف نتائج {sectionName} — {termName}";

        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            return File(_export.ToPdf(title, $"عدد الطلاب: {rows.Count}", columns, data, settings.SchoolName),
                "application/pdf", $"results-{sectionId}-{termId}.pdf");

        return File(_export.ToExcel(title, columns, data),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"results-{sectionId}-{termId}.xlsx");
    }
}
