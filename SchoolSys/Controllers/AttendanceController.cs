using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Helpers;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[HasPermission(Permissions.AttendanceView)]
public class AttendanceController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notify;
    private readonly ICurrentUserService _user;
    private readonly IAuditService _audit;
    private readonly IExportService _export;

    public AttendanceController(ApplicationDbContext db, INotificationService notify,
        ICurrentUserService user, IAuditService audit, IExportService export)
    {
        _db = db;
        _notify = notify;
        _user = user;
        _audit = audit;
        _export = export;
    }

    // ==================================================================
    // تسجيل حضور الطلاب
    // ==================================================================
    public async Task<IActionResult> Index(int? sectionId, DateTime? date)
    {
        var year = await GetCurrentYearAsync();
        var settings = await GetSettingsAsync();
        var day = (date ?? DateTime.Today).Date;

        var vm = new TakeAttendanceViewModel { SectionId = sectionId, Date = day };

        // المعلم يرى شعبه فقط
        var sectionQuery = _db.Sections.AsNoTracking()
            .Where(s => s.IsActive && (year == null || s.AcademicYearId == year.Id));

        if (User.IsInRole(RoleNames.Teacher) && !User.Can(Permissions.StudentsCreate))
        {
            var employeeId = await _user.GetEmployeeIdAsync();
            var mySections = await _db.TeacherSubjects
                .Where(ts => ts.TeacherId == employeeId && (year == null || ts.AcademicYearId == year.Id))
                .Select(ts => ts.SectionId)
                .Distinct()
                .ToListAsync();

            var homeroom = await _db.Sections
                .Where(s => s.HomeroomTeacherId == employeeId)
                .Select(s => s.Id)
                .ToListAsync();

            mySections.AddRange(homeroom);
            sectionQuery = sectionQuery.Where(s => mySections.Contains(s.Id));
        }

        vm.Sections = await sectionQuery
            .OrderBy(s => s.Grade.SeqNo).ThenBy(s => s.Name)
            .Select(s => new SelectListItem(s.Grade.Name + " - " + s.Name, s.Id.ToString(), s.Id == sectionId))
            .ToListAsync();

        if (sectionId is null && vm.Sections.Count > 0)
        {
            sectionId = int.Parse(vm.Sections[0].Value);
            vm.SectionId = sectionId;
            vm.Sections[0].Selected = true;
        }

        if (sectionId is null) return View(vm);

        vm.SectionName = await _db.Sections.Where(s => s.Id == sectionId)
            .Select(s => s.Grade.Name + " - " + s.Name).FirstOrDefaultAsync();

        if (day > DateTime.Today)
        {
            vm.IsLocked = true;
            vm.LockReason = "لا يمكن تسجيل الحضور لتاريخ مستقبلي.";
        }

        var students = await _db.Students.AsNoTracking()
            .Where(s => s.CurrentSectionId == sectionId && s.Status == StudentStatus.Active)
            .OrderBy(s => s.FullName)
            .Select(s => new { s.Id, s.StudentNo, s.FullName, s.PhotoPath })
            .ToListAsync();

        var existing = await _db.StudentAttendances.AsNoTracking()
            .Where(a => a.SectionId == sectionId && a.Date == day)
            .ToDictionaryAsync(a => a.StudentId);

        vm.AlreadyRecorded = existing.Count > 0;

        // نسبة الحضور التاريخية لكل طالب
        var studentIds = students.Select(s => s.Id).ToList();
        var history = await _db.StudentAttendances.AsNoTracking()
            .Where(a => studentIds.Contains(a.StudentId))
            .GroupBy(a => a.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Total = g.Count(),
                Present = g.Count(x => x.Status == AttendanceStatus.Present || x.Status == AttendanceStatus.Late)
            })
            .ToDictionaryAsync(x => x.StudentId);

        vm.Entries = students.Select(s =>
        {
            existing.TryGetValue(s.Id, out var e);
            history.TryGetValue(s.Id, out var h);

            return new AttendanceEntry
            {
                StudentId = s.Id,
                StudentNo = s.StudentNo,
                StudentName = s.FullName,
                PhotoPath = s.PhotoPath,
                Status = e?.Status ?? AttendanceStatus.Present,
                CheckInTime = e?.CheckInTime,
                LateMinutes = e?.LateMinutes ?? 0,
                Notes = e?.Notes,
                ExistingId = e?.Id,
                HistoryRate = h is { Total: > 0 } ? Math.Round((double)h.Present / h.Total * 100, 1) : 100
            };
        }).ToList();

        ViewBag.GraceMinutes = settings.LateGraceMinutes;
        ViewBag.SchoolStart = settings.SchoolStartTime;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AttendanceTakeStudents)]
    public async Task<IActionResult> Save(int sectionId, DateTime date, List<AttendanceEntry> entries)
    {
        if (date.Date > DateTime.Today)
        {
            Error("لا يمكن تسجيل الحضور لتاريخ مستقبلي.");
            return RedirectToAction(nameof(Index), new { sectionId, date = date.ToString("yyyy-MM-dd") });
        }

        var year = await GetCurrentYearAsync();
        if (year is null)
        {
            Error("لا يوجد عام دراسي محدد.");
            return RedirectToAction(nameof(Index), new { sectionId });
        }

        var settings = await GetSettingsAsync();
        var day = date.Date;
        var userId = _user.UserId;

        var existing = await _db.StudentAttendances
            .Where(a => a.SectionId == sectionId && a.Date == day)
            .ToDictionaryAsync(a => a.StudentId);

        var newlyAbsent = new List<int>();

        foreach (var entry in entries)
        {
            if (existing.TryGetValue(entry.StudentId, out var record))
            {
                var wasAbsent = record.Status == AttendanceStatus.Absent;
                record.Status = entry.Status;
                record.CheckInTime = entry.CheckInTime;
                record.LateMinutes = entry.LateMinutes;
                record.Notes = entry.Notes;

                if (!wasAbsent && entry.Status == AttendanceStatus.Absent && !record.GuardianNotified)
                    newlyAbsent.Add(entry.StudentId);
            }
            else
            {
                var record2 = new StudentAttendance
                {
                    StudentId = entry.StudentId,
                    SectionId = sectionId,
                    AcademicYearId = year.Id,
                    Date = day,
                    Status = entry.Status,
                    CheckInTime = entry.CheckInTime,
                    LateMinutes = entry.LateMinutes,
                    Notes = entry.Notes,
                    Method = AttendanceMethod.Manual,
                    RecordedByUserId = userId
                };
                _db.StudentAttendances.Add(record2);

                if (entry.Status == AttendanceStatus.Absent)
                    newlyAbsent.Add(entry.StudentId);
            }
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("تسجيل حضور الطلاب", nameof(StudentAttendance), sectionId,
            $"التاريخ {day:yyyy-MM-dd} — {entries.Count} طالب");

        // إشعار أولياء أمور الغائبين
        if (settings.AutoNotifyGuardianOnAbsence && newlyAbsent.Count > 0)
        {
            var names = await _db.Students.Where(s => newlyAbsent.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.FullName);

            foreach (var studentId in newlyAbsent)
            {
                await _notify.NotifyGuardiansOfStudentAsync(studentId,
                    "غياب الطالب",
                    $"نفيدكم بغياب الطالب {names.GetValueOrDefault(studentId)} بتاريخ {day:yyyy/MM/dd}.",
                    NotificationType.Attendance, NotificationSeverity.Warning);
            }

            var toMark = await _db.StudentAttendances
                .Where(a => a.Date == day && newlyAbsent.Contains(a.StudentId))
                .ToListAsync();
            foreach (var a in toMark) a.GuardianNotified = true;
            await _db.SaveChangesAsync();
        }

        Success($"تم حفظ الحضور لـ {entries.Count} طالب." +
                (newlyAbsent.Count > 0 ? $" وتم إشعار أولياء أمور {newlyAbsent.Count} طالب غائب." : ""));

        return RedirectToAction(nameof(Index), new { sectionId, date = day.ToString("yyyy-MM-dd") });
    }

    // ==================================================================
    // حضور الموظفين
    // ==================================================================
    [HasPermission(Permissions.AttendanceTakeStaff)]
    public async Task<IActionResult> Staff(DateTime? date, EmployeeType? type)
    {
        var day = (date ?? DateTime.Today).Date;
        var vm = new StaffAttendanceViewModel { Date = day, Type = type };

        var query = _db.Employees.AsNoTracking().Where(e => e.IsActive);
        if (type.HasValue) query = query.Where(e => e.EmployeeType == type);

        var employees = await query.OrderBy(e => e.EmployeeNo)
            .Select(e => new { e.Id, e.EmployeeNo, e.FullName, e.EmployeeType, e.PhotoPath })
            .ToListAsync();

        var existing = await _db.StaffAttendances.AsNoTracking()
            .Where(a => a.Date == day)
            .ToDictionaryAsync(a => a.EmployeeId);

        vm.Entries = employees.Select(e =>
        {
            existing.TryGetValue(e.Id, out var rec);
            return new StaffAttendanceEntry
            {
                EmployeeId = e.Id,
                EmployeeNo = e.EmployeeNo,
                FullName = e.FullName,
                EmployeeType = e.EmployeeType,
                PhotoPath = e.PhotoPath,
                Status = rec?.Status ?? AttendanceStatus.Present,
                CheckInTime = rec?.CheckInTime,
                CheckOutTime = rec?.CheckOutTime,
                LateMinutes = rec?.LateMinutes ?? 0,
                Notes = rec?.Notes,
                ExistingId = rec?.Id
            };
        }).ToList();

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AttendanceTakeStaff)]
    public async Task<IActionResult> SaveStaff(DateTime date, List<StaffAttendanceEntry> entries)
    {
        var day = date.Date;
        if (day > DateTime.Today)
        {
            Error("لا يمكن تسجيل الحضور لتاريخ مستقبلي.");
            return RedirectToAction(nameof(Staff));
        }

        var userId = _user.UserId;
        var existing = await _db.StaffAttendances.Where(a => a.Date == day).ToDictionaryAsync(a => a.EmployeeId);

        foreach (var entry in entries)
        {
            if (existing.TryGetValue(entry.EmployeeId, out var rec))
            {
                rec.Status = entry.Status;
                rec.CheckInTime = entry.CheckInTime;
                rec.CheckOutTime = entry.CheckOutTime;
                rec.LateMinutes = entry.LateMinutes;
                rec.Notes = entry.Notes;
            }
            else
            {
                _db.StaffAttendances.Add(new StaffAttendance
                {
                    EmployeeId = entry.EmployeeId,
                    Date = day,
                    Status = entry.Status,
                    CheckInTime = entry.CheckInTime,
                    CheckOutTime = entry.CheckOutTime,
                    LateMinutes = entry.LateMinutes,
                    Notes = entry.Notes,
                    Method = AttendanceMethod.Manual,
                    RecordedByUserId = userId
                });
            }
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("تسجيل حضور الموظفين", nameof(StaffAttendance), null, $"{entries.Count} موظف");

        Success($"تم حفظ حضور {entries.Count} موظف.");
        return RedirectToAction(nameof(Staff), new { date = day.ToString("yyyy-MM-dd") });
    }

    // ==================================================================
    // التسجيل عبر QR
    // ==================================================================
    [HasPermission(Permissions.AttendanceTakeStudents)]
    public async Task<IActionResult> Scan()
    {
        var today = DateTime.Today;
        var vm = new ScanViewModel
        {
            Date = today,
            TodayScans = await _db.StudentAttendances
                .CountAsync(a => a.Date == today && a.Method == AttendanceMethod.QrCode)
        };

        vm.Recent = await _db.StudentAttendances.AsNoTracking()
            .Where(a => a.Date == today && a.Method == AttendanceMethod.QrCode)
            .OrderByDescending(a => a.Id)
            .Take(15)
            .Select(a => new ScanResultRow
            {
                Name = a.Student.FullName,
                Code = a.Student.StudentNo,
                Section = a.Section.Grade.Name + " - " + a.Section.Name,
                Status = a.Status,
                Time = a.CheckInTime ?? TimeSpan.Zero
            })
            .ToListAsync();

        return View(vm);
    }

    /// <summary>نقطة نهاية JSON يستدعيها الماسح الضوئي.</summary>
    [HttpPost, HasPermission(Permissions.AttendanceTakeStudents)]
    public async Task<IActionResult> ScanRecord([FromBody] ScanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return Json(new { success = false, message = "رمز غير صالح." });

        var code = request.Code.Trim();
        var token = code.StartsWith("STU:", StringComparison.OrdinalIgnoreCase) ? code[4..] : code;

        var year = await GetCurrentYearAsync();
        var settings = await GetSettingsAsync();
        var today = DateTime.Today;
        var now = DateTime.Now.TimeOfDay;

        var student = await _db.Students
            .Include(s => s.CurrentSection).ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(s => s.QrToken == token || s.StudentNo == token);

        if (student is null)
            return Json(new { success = false, message = "لم يتم العثور على الطالب." });

        if (student.CurrentSectionId is null)
            return Json(new { success = false, message = $"{student.FullName}: غير مسجّل في شعبة." });

        if (year is null)
            return Json(new { success = false, message = "لا يوجد عام دراسي محدد." });

        var existing = await _db.StudentAttendances
            .FirstOrDefaultAsync(a => a.StudentId == student.Id && a.Date == today);

        // حساب التأخير مقابل بداية الدوام مع فترة السماح
        var graceEnd = settings.SchoolStartTime.Add(TimeSpan.FromMinutes(settings.LateGraceMinutes));
        var isLate = now > graceEnd;
        var lateMinutes = isLate ? (int)(now - settings.SchoolStartTime).TotalMinutes : 0;
        var status = isLate ? AttendanceStatus.Late : AttendanceStatus.Present;

        string message;
        if (existing is null)
        {
            _db.StudentAttendances.Add(new StudentAttendance
            {
                StudentId = student.Id,
                SectionId = student.CurrentSectionId.Value,
                AcademicYearId = year.Id,
                Date = today,
                Status = status,
                CheckInTime = now,
                LateMinutes = lateMinutes,
                Method = AttendanceMethod.QrCode,
                RecordedByUserId = _user.UserId
            });
            message = isLate ? $"تم تسجيل الحضور متأخراً ({lateMinutes} دقيقة)" : "تم تسجيل الحضور";
        }
        else if (existing.CheckOutTime is null && existing.CheckInTime is not null)
        {
            existing.CheckOutTime = now;
            message = "تم تسجيل الانصراف";
        }
        else
        {
            existing.Status = status;
            existing.CheckInTime = now;
            existing.LateMinutes = lateMinutes;
            existing.Method = AttendanceMethod.QrCode;
            message = "تم تحديث سجل الحضور";
        }

        await _db.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message,
            name = student.FullName,
            code = student.StudentNo,
            section = $"{student.CurrentSection!.Grade.Name} - {student.CurrentSection.Name}",
            status = status.Display(),
            photo = student.PhotoPath,
            time = DateTime.Now.ToString("hh:mm tt")
        });
    }

    public record ScanRequest(string Code);

    // ==================================================================
    // التقارير
    // ==================================================================
    [HasPermission(Permissions.AttendanceReports)]
    public async Task<IActionResult> Reports(AttendanceReportViewModel filter)
    {
        var vm = filter;
        if (vm.From == default) vm.From = DateTime.Today.AddDays(-30);
        if (vm.To == default) vm.To = DateTime.Today;

        var year = await GetCurrentYearAsync();

        vm.Stages = await _db.Stages.AsNoTracking().OrderBy(s => s.SeqNo)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString(), s.Id == vm.StageId)).ToListAsync();

        vm.Grades = await _db.Grades.AsNoTracking()
            .Where(g => vm.StageId == null || g.StageId == vm.StageId)
            .OrderBy(g => g.SeqNo)
            .Select(g => new SelectListItem(g.Name, g.Id.ToString(), g.Id == vm.GradeId)).ToListAsync();

        vm.Sections = await _db.Sections.AsNoTracking()
            .Where(s => s.IsActive && (year == null || s.AcademicYearId == year.Id)
                        && (vm.GradeId == null || s.GradeId == vm.GradeId)
                        && (vm.StageId == null || s.Grade.StageId == vm.StageId))
            .OrderBy(s => s.Grade.SeqNo).ThenBy(s => s.Name)
            .Select(s => new SelectListItem(s.Grade.Name + " - " + s.Name, s.Id.ToString(), s.Id == vm.SectionId))
            .ToListAsync();

        var query = _db.StudentAttendances.AsNoTracking()
            .Where(a => a.Date >= vm.From.Date && a.Date <= vm.To.Date);

        if (vm.SectionId.HasValue) query = query.Where(a => a.SectionId == vm.SectionId);
        else if (vm.GradeId.HasValue) query = query.Where(a => a.Section.GradeId == vm.GradeId);
        else if (vm.StageId.HasValue) query = query.Where(a => a.Section.Grade.StageId == vm.StageId);

        var totals = await query.GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        vm.TotalPresent = totals.FirstOrDefault(t => t.Status == AttendanceStatus.Present)?.Count ?? 0;
        vm.TotalAbsent = totals.FirstOrDefault(t => t.Status == AttendanceStatus.Absent)?.Count ?? 0;
        vm.TotalLate = totals.FirstOrDefault(t => t.Status == AttendanceStatus.Late)?.Count ?? 0;
        vm.TotalExcused = totals.FirstOrDefault(t => t.Status == AttendanceStatus.Excused)?.Count ?? 0;
        vm.TotalRecords = totals.Sum(t => t.Count);

        vm.Rows = vm.GroupBy switch
        {
            "section" => await query
                .GroupBy(a => new { a.SectionId, Name = a.Section.Grade.Name + " - " + a.Section.Name })
                .Select(g => new AttendanceReportRow
                {
                    Id = g.Key.SectionId,
                    Label = g.Key.Name,
                    Present = g.Count(x => x.Status == AttendanceStatus.Present),
                    Absent = g.Count(x => x.Status == AttendanceStatus.Absent),
                    Late = g.Count(x => x.Status == AttendanceStatus.Late),
                    Excused = g.Count(x => x.Status == AttendanceStatus.Excused)
                })
                .OrderBy(r => r.Label)
                .ToListAsync(),

            "day" => await query
                .GroupBy(a => a.Date)
                .Select(g => new AttendanceReportRow
                {
                    Label = g.Key.ToString("yyyy/MM/dd"),
                    Present = g.Count(x => x.Status == AttendanceStatus.Present),
                    Absent = g.Count(x => x.Status == AttendanceStatus.Absent),
                    Late = g.Count(x => x.Status == AttendanceStatus.Late),
                    Excused = g.Count(x => x.Status == AttendanceStatus.Excused)
                })
                .OrderByDescending(r => r.Label)
                .ToListAsync(),

            _ => await query
                .GroupBy(a => new
                {
                    a.StudentId,
                    a.Student.FullName,
                    a.Student.StudentNo,
                    Section = a.Section.Grade.Name + " - " + a.Section.Name
                })
                .Select(g => new AttendanceReportRow
                {
                    Id = g.Key.StudentId,
                    Label = g.Key.FullName,
                    SubLabel = g.Key.StudentNo + " · " + g.Key.Section,
                    Present = g.Count(x => x.Status == AttendanceStatus.Present),
                    Absent = g.Count(x => x.Status == AttendanceStatus.Absent),
                    Late = g.Count(x => x.Status == AttendanceStatus.Late),
                    Excused = g.Count(x => x.Status == AttendanceStatus.Excused)
                })
                .OrderByDescending(r => r.Absent)
                .Take(500)
                .ToListAsync()
        };

        var trend = await query.GroupBy(a => a.Date)
            .Select(g => new
            {
                Date = g.Key,
                Total = g.Count(),
                Present = g.Count(x => x.Status == AttendanceStatus.Present || x.Status == AttendanceStatus.Late)
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        vm.DailyTrend = trend.TakeLast(20)
            .Select(t => new ChartPoint(t.Date.ToString("MM/dd"),
                t.Total > 0 ? Math.Round((decimal)t.Present / t.Total * 100m, 1) : 0))
            .ToList();

        return View(vm);
    }

    [HasPermission(Permissions.ReportsExport)]
    public async Task<IActionResult> ExportReport(AttendanceReportViewModel filter, string format = "excel")
    {
        var result = await Reports(filter) as ViewResult;
        var vm = result?.Model as AttendanceReportViewModel ?? filter;
        var settings = await GetSettingsAsync();

        var columns = new List<ExportColumn>
        {
            new("البيان", 2.4f), new("التفاصيل", 1.8f), new("حاضر", .8f), new("غائب", .8f),
            new("متأخر", .8f), new("بعذر", .8f), new("الإجمالي", .9f), new("النسبة %", .9f)
        };

        var data = vm.Rows.Select(r => new string?[]
        {
            r.Label, r.SubLabel, r.Present.ToString(), r.Absent.ToString(),
            r.Late.ToString(), r.Excused.ToString(), r.Total.ToString(), r.Rate.ToString("0.#") + "%"
        });

        var title = $"تقرير الحضور من {vm.From:yyyy/MM/dd} إلى {vm.To:yyyy/MM/dd}";

        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            return File(_export.ToPdf(title, $"نسبة الحضور العامة: {vm.OverallRate:0.#}%", columns, data,
                    settings.SchoolName, logoPath: settings.LogoPath),
                "application/pdf", $"attendance-{DateTime.Now:yyyyMMdd}.pdf");

        return File(_export.ToExcel(title, columns, data),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"attendance-{DateTime.Now:yyyyMMdd}.xlsx");
    }
}
