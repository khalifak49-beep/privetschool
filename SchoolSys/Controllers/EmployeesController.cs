using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Helpers;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[HasPermission(Permissions.EmployeesView)]
public class EmployeesController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IFileStorageService _files;
    private readonly IAuditService _audit;
    private readonly IExportService _export;

    public EmployeesController(ApplicationDbContext db, UserManager<ApplicationUser> users,
        IFileStorageService files, IAuditService audit, IExportService export)
    {
        _db = db;
        _users = users;
        _files = files;
        _audit = audit;
        _export = export;
    }

    public async Task<IActionResult> Index(string? q, EmployeeType? type, bool? isActive, int page = 1)
    {
        var vm = new EmployeeIndexViewModel { Q = q, Type = type, IsActive = isActive, Page = page };
        var year = await GetCurrentYearAsync();

        var query = _db.Employees.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(e => e.FullName.Contains(term)
                                     || e.EmployeeNo.Contains(term)
                                     || (e.Phone != null && e.Phone.Contains(term))
                                     || (e.Specialization != null && e.Specialization.Contains(term)));
        }

        if (type.HasValue) query = query.Where(e => e.EmployeeType == type);
        if (isActive.HasValue) query = query.Where(e => e.IsActive == isActive);

        var projected = query.Select(e => new EmployeeListRow
        {
            Id = e.Id,
            EmployeeNo = e.EmployeeNo,
            FullName = e.FullName,
            PhotoPath = e.PhotoPath,
            EmployeeType = e.EmployeeType,
            Specialization = e.Specialization,
            Phone = e.Phone,
            HireDate = e.HireDate,
            IsActive = e.IsActive,
            HasAccount = _db.Users.Any(u => u.EmployeeId == e.Id),
            SectionsCount = e.TeacherSubjects
                .Where(ts => year == null || ts.AcademicYearId == year.Id)
                .Select(ts => ts.SectionId).Distinct().Count()
        });

        vm.Employees = await PagedList<EmployeeListRow>.CreateAsync(
            projected.OrderBy(e => e.EmployeeNo), page, 25);

        vm.TotalCount = await _db.Employees.CountAsync();
        vm.TeachersCount = await _db.Employees.CountAsync(e => e.EmployeeType == EmployeeType.Teacher);
        vm.ActiveCount = await _db.Employees.CountAsync(e => e.IsActive);

        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id);
        if (employee is null) return NotFound();

        var year = await GetCurrentYearAsync();
        var settings = await GetSettingsAsync();
        var account = await _db.Users.FirstOrDefaultAsync(u => u.EmployeeId == id);

        var vm = new EmployeeDetailsViewModel
        {
            Employee = employee,
            HasAccount = account is not null,
            AccountEmail = account?.Email,
            Currency = settings.Currency
        };

        vm.TeachingLoad = await _db.TeacherSubjects
            .Where(ts => ts.TeacherId == id && (year == null || ts.AcademicYearId == year.Id))
            .Select(ts => new TeachingLoadRow
            {
                Id = ts.Id,
                Subject = ts.Subject.Name,
                Section = ts.Section.Grade.Name + " - " + ts.Section.Name,
                SectionId = ts.SectionId,
                Students = ts.Section.Enrollments.Count(e => e.IsActive)
            })
            .OrderBy(t => t.Section)
            .ToListAsync();

        vm.Timetable = await _db.TimetableSlots
            .Where(t => t.TeacherId == id && (year == null || t.AcademicYearId == year.Id))
            .OrderBy(t => t.DayOfWeek).ThenBy(t => t.PeriodNo)
            .Select(t => new TodayLessonRow
            {
                PeriodNo = t.PeriodNo,
                Subject = t.Subject.Name,
                Section = t.Section.Grade.Name + " - " + t.Section.Name,
                SectionId = t.SectionId,
                Room = t.Room,
                StartTime = t.StartTime,
                EndTime = t.EndTime
            })
            .ToListAsync();

        // اليوم يُخزَّن ضمن TodayLessonRow.PeriodNo فقط، لذا نُعيد الجدول مجمّعاً في العرض
        ViewBag.TimetableByDay = await _db.TimetableSlots
            .Where(t => t.TeacherId == id && (year == null || t.AcademicYearId == year.Id))
            .OrderBy(t => t.DayOfWeek).ThenBy(t => t.PeriodNo)
            .Select(t => new
            {
                t.DayOfWeek,
                t.PeriodNo,
                Subject = t.Subject.Name,
                Section = t.Section.Grade.Name + " - " + t.Section.Name,
                t.StartTime,
                t.EndTime
            })
            .ToListAsync();

        vm.HomeroomSections = await _db.Sections
            .Include(s => s.Grade)
            .Where(s => s.HomeroomTeacherId == id && (year == null || s.AcademicYearId == year.Id))
            .ToListAsync();

        var att = await _db.StaffAttendances
            .Where(a => a.EmployeeId == id)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        vm.AttendancePresent = att.FirstOrDefault(a => a.Status == AttendanceStatus.Present)?.Count ?? 0;
        vm.AttendanceAbsent = att.FirstOrDefault(a => a.Status == AttendanceStatus.Absent)?.Count ?? 0;
        vm.AttendanceLate = att.FirstOrDefault(a => a.Status == AttendanceStatus.Late)?.Count ?? 0;

        return View(vm);
    }

    [HasPermission(Permissions.EmployeesCreate)]
    public async Task<IActionResult> Create()
        => View("Form", new EmployeeFormViewModel { EmployeeNo = await NextEmployeeNoAsync() });

    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.EmployeesCreate)]
    public async Task<IActionResult> Create(EmployeeFormViewModel vm)
    {
        if (!ModelState.IsValid) return View("Form", vm);

        var no = string.IsNullOrWhiteSpace(vm.EmployeeNo) ? await NextEmployeeNoAsync() : vm.EmployeeNo.Trim();
        if (await _db.Employees.AnyAsync(e => e.EmployeeNo == no))
        {
            ModelState.AddModelError(nameof(vm.EmployeeNo), "الرقم الوظيفي مستخدم مسبقاً.");
            return View("Form", vm);
        }

        var employee = new Employee
        {
            EmployeeNo = no,
            FullName = vm.FullName.Trim(),
            EmployeeType = vm.EmployeeType,
            NationalId = vm.NationalId,
            Gender = vm.Gender,
            BirthDate = vm.BirthDate,
            Phone = vm.Phone,
            Email = vm.Email,
            Address = vm.Address,
            HireDate = vm.HireDate,
            Specialization = vm.Specialization,
            Qualification = vm.Qualification,
            Salary = vm.Salary,
            IsActive = vm.IsActive
        };

        try
        {
            employee.PhotoPath = await _files.SaveAsync(vm.Photo, "employees", IFileStorageService.ImageExtensions, 3 * 1024 * 1024);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(vm.Photo), ex.Message);
            return View("Form", vm);
        }

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("إضافة موظف", nameof(Employee), employee.Id, employee.FullName);

        Success($"تمت إضافة «{employee.FullName}» برقم وظيفي {employee.EmployeeNo}.");
        return RedirectToAction(nameof(Details), new { id = employee.Id });
    }

    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await _db.Employees.FindAsync(id);
        if (e is null) return NotFound();

        return View("Form", new EmployeeFormViewModel
        {
            Id = e.Id,
            EmployeeNo = e.EmployeeNo,
            FullName = e.FullName,
            EmployeeType = e.EmployeeType,
            NationalId = e.NationalId,
            Gender = e.Gender,
            BirthDate = e.BirthDate,
            Phone = e.Phone,
            Email = e.Email,
            Address = e.Address,
            HireDate = e.HireDate,
            Specialization = e.Specialization,
            Qualification = e.Qualification,
            Salary = e.Salary,
            IsActive = e.IsActive,
            PhotoPath = e.PhotoPath
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> Edit(EmployeeFormViewModel vm)
    {
        if (!ModelState.IsValid) return View("Form", vm);

        var e = await _db.Employees.FindAsync(vm.Id);
        if (e is null) return NotFound();

        e.FullName = vm.FullName.Trim();
        e.EmployeeType = vm.EmployeeType;
        e.NationalId = vm.NationalId;
        e.Gender = vm.Gender;
        e.BirthDate = vm.BirthDate;
        e.Phone = vm.Phone;
        e.Email = vm.Email;
        e.Address = vm.Address;
        e.HireDate = vm.HireDate;
        e.Specialization = vm.Specialization;
        e.Qualification = vm.Qualification;
        e.Salary = vm.Salary;
        e.IsActive = vm.IsActive;

        if (vm.Photo is not null)
        {
            try
            {
                var path = await _files.SaveAsync(vm.Photo, "employees", IFileStorageService.ImageExtensions, 3 * 1024 * 1024);
                if (path is not null)
                {
                    _files.Delete(e.PhotoPath);
                    e.PhotoPath = path;
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(vm.Photo), ex.Message);
                vm.PhotoPath = e.PhotoPath;
                return View("Form", vm);
            }
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("تعديل موظف", nameof(Employee), e.Id, e.FullName);

        Success("تم حفظ بيانات الموظف.");
        return RedirectToAction(nameof(Details), new { id = e.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.EmployeesDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var e = await _db.Employees.FindAsync(id);
        if (e is null) return NotFound();

        if (await _db.TeacherSubjects.AnyAsync(ts => ts.TeacherId == id) ||
            await _db.TimetableSlots.AnyAsync(t => t.TeacherId == id) ||
            await _db.Sections.AnyAsync(s => s.HomeroomTeacherId == id) ||
            await _db.Users.AnyAsync(u => u.EmployeeId == id))
        {
            Error("لا يمكن حذف الموظف لارتباطه بجداول أو شعب أو حساب مستخدم. يمكنك تعطيله بدلاً من الحذف.");
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            _files.Delete(e.PhotoPath);
            _db.Employees.Remove(e);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("حذف موظف", nameof(Employee), id, e.FullName);
            Success("تم حذف الموظف.");
        }
        catch (DbUpdateException)
        {
            Error("تعذّر حذف الموظف لارتباطه بسجلات أخرى.");
            return RedirectToAction(nameof(Details), new { id });
        }

        return RedirectToAction(nameof(Index));
    }

    // ---------------- إنشاء حساب ----------------
    [HasPermission(Permissions.UsersManage)]
    public async Task<IActionResult> CreateAccount(int id)
    {
        var e = await _db.Employees.FindAsync(id);
        if (e is null) return NotFound();

        if (await _db.Users.AnyAsync(u => u.EmployeeId == id))
        {
            Warning("يوجد حساب مرتبط بهذا الموظف بالفعل.");
            return RedirectToAction(nameof(Details), new { id });
        }

        return View("~/Views/Shared/_CreateAccount.cshtml", new CreateAccountViewModel
        {
            EntityId = e.Id,
            EntityName = e.FullName,
            EntityType = "Employee",
            Email = e.Email ?? $"{e.EmployeeNo.ToLowerInvariant()}@school.local",
            Role = DefaultRoleFor(e.EmployeeType)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.UsersManage)]
    public async Task<IActionResult> CreateAccount(CreateAccountViewModel vm)
    {
        if (!ModelState.IsValid)
            return View("~/Views/Shared/_CreateAccount.cshtml", vm);

        var e = await _db.Employees.FindAsync(vm.EntityId);
        if (e is null) return NotFound();

        var user = new ApplicationUser
        {
            UserName = vm.Email,
            Email = vm.Email,
            EmailConfirmed = true,
            FullName = e.FullName,
            PhoneNumber = e.Phone,
            PhotoPath = e.PhotoPath,
            EmployeeId = e.Id,
            IsActive = true
        };

        var result = await _users.CreateAsync(user, vm.Password);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
                ModelState.AddModelError(string.Empty, AccountController.TranslateIdentityError(err));
            return View("~/Views/Shared/_CreateAccount.cshtml", vm);
        }

        var role = string.IsNullOrWhiteSpace(vm.Role) ? DefaultRoleFor(e.EmployeeType) : vm.Role;
        await _users.AddToRoleAsync(user, role);
        await _audit.LogAsync("إنشاء حساب موظف", nameof(Employee), e.Id, vm.Email);

        Success($"تم إنشاء الحساب «{vm.Email}» بدور {RoleNames.Display(role)}.");
        return RedirectToAction(nameof(Details), new { id = e.Id });
    }

    private static string DefaultRoleFor(EmployeeType type) => type switch
    {
        EmployeeType.Teacher => RoleNames.Teacher,
        EmployeeType.Accountant => RoleNames.Accountant,
        EmployeeType.Receptionist => RoleNames.Receptionist,
        EmployeeType.Driver or EmployeeType.BusSupervisor => RoleNames.TransportManager,
        _ => RoleNames.AcademicAdmin
    };

    [HasPermission(Permissions.ReportsExport)]
    public async Task<IActionResult> Export(string? q, EmployeeType? type, string format = "excel")
    {
        var query = _db.Employees.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(e => e.FullName.Contains(term) || e.EmployeeNo.Contains(term));
        }
        if (type.HasValue) query = query.Where(e => e.EmployeeType == type);

        var rows = await query.OrderBy(e => e.EmployeeNo).Take(5000).ToListAsync();
        var settings = await GetSettingsAsync();

        var columns = new List<ExportColumn>
        {
            new("الرقم الوظيفي", 1f), new("الاسم", 2.4f), new("الوظيفة", 1.1f),
            new("التخصص", 1.4f), new("المؤهل", 1.1f), new("الجوال", 1.1f),
            new("تاريخ التعيين", 1.1f), new("الحالة", .9f)
        };

        var data = rows.Select(e => new string?[]
        {
            e.EmployeeNo, e.FullName, e.EmployeeType.Display(), e.Specialization,
            e.Qualification, e.Phone, e.HireDate.ToString("yyyy/MM/dd"), e.IsActive ? "نشط" : "معطّل"
        });

        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            return File(_export.ToPdf("كشف الموظفين", $"عدد السجلات: {rows.Count}", columns, data,
                    settings.SchoolName, logoPath: settings.LogoPath),
                "application/pdf", $"employees-{DateTime.Now:yyyyMMdd}.pdf");

        return File(_export.ToExcel("كشف الموظفين", columns, data),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"employees-{DateTime.Now:yyyyMMdd}.xlsx");
    }

    private async Task<string> NextEmployeeNoAsync()
    {
        var last = await _db.Employees
            .Where(e => e.EmployeeNo.StartsWith("EMP"))
            .OrderByDescending(e => e.EmployeeNo)
            .Select(e => e.EmployeeNo)
            .FirstOrDefaultAsync();

        var n = 1;
        if (last is { Length: > 3 } && int.TryParse(last[3..], out var parsed)) n = parsed + 1;
        return "EMP" + n.ToString("D4");
    }
}
