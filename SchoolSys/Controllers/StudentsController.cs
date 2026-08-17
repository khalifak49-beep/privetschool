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

[HasPermission(Permissions.StudentsView)]
public class StudentsController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _files;
    private readonly IAuditService _audit;
    private readonly IQrService _qr;
    private readonly IExportService _export;
    private readonly INotificationService _notify;

    public StudentsController(ApplicationDbContext db, IFileStorageService files, IAuditService audit,
        IQrService qr, IExportService export, INotificationService notify)
    {
        _db = db;
        _files = files;
        _audit = audit;
        _qr = qr;
        _export = export;
        _notify = notify;
    }

    // ==================================================================
    // القائمة
    // ==================================================================
    public async Task<IActionResult> Index(StudentFilter filter)
    {
        var vm = new StudentIndexViewModel { Filter = filter };
        var query = BuildQuery(filter);

        vm.Students = await PagedList<StudentListRow>.CreateAsync(
            ApplySort(query, filter.Sort), filter.Page, filter.PageSize);

        vm.ActiveCount = await _db.Students.CountAsync(s => s.Status == StudentStatus.Active);
        vm.MaleCount = await _db.Students.CountAsync(s => s.Status == StudentStatus.Active && s.Gender == Gender.Male);
        vm.FemaleCount = await _db.Students.CountAsync(s => s.Status == StudentStatus.Active && s.Gender == Gender.Female);

        await FillFiltersAsync(vm);
        return View(vm);
    }

    private IQueryable<StudentListRow> BuildQuery(StudentFilter f)
    {
        var q = _db.Students.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var term = f.Q.Trim();
            q = q.Where(s => s.FullName.Contains(term)
                             || s.StudentNo.Contains(term)
                             || (s.NationalId != null && s.NationalId.Contains(term))
                             || (s.Phone != null && s.Phone.Contains(term)));
        }

        if (f.SectionId.HasValue) q = q.Where(s => s.CurrentSectionId == f.SectionId);
        else if (f.GradeId.HasValue) q = q.Where(s => s.CurrentSection!.GradeId == f.GradeId);
        else if (f.StageId.HasValue) q = q.Where(s => s.CurrentSection!.Grade.StageId == f.StageId);

        if (f.Status.HasValue) q = q.Where(s => s.Status == f.Status);
        if (f.Gender.HasValue) q = q.Where(s => s.Gender == f.Gender);

        return q.Select(s => new StudentListRow
        {
            Id = s.Id,
            StudentNo = s.StudentNo,
            FullName = s.FullName,
            PhotoPath = s.PhotoPath,
            Gender = s.Gender,
            Section = s.CurrentSection != null ? s.CurrentSection.Grade.Name + " - " + s.CurrentSection.Name : null,
            Stage = s.CurrentSection != null ? s.CurrentSection.Grade.Stage.Name : null,
            Status = s.Status,
            EnrollmentDate = s.EnrollmentDate,
            GuardianName = s.StudentGuardians.Where(g => g.IsPrimary).Select(g => g.Guardian.FullName).FirstOrDefault()
                           ?? s.StudentGuardians.Select(g => g.Guardian.FullName).FirstOrDefault(),
            GuardianPhone = s.StudentGuardians.Where(g => g.IsPrimary).Select(g => g.Guardian.Phone).FirstOrDefault()
                            ?? s.StudentGuardians.Select(g => g.Guardian.Phone).FirstOrDefault(),
            Outstanding = s.Invoices.Where(i => i.Status != InvoiceStatus.Cancelled)
                              .Sum(i => (decimal?)(i.NetAmount - i.PaidAmount)) ?? 0m
        });
    }

    private static IQueryable<StudentListRow> ApplySort(IQueryable<StudentListRow> q, string? sort) => sort switch
    {
        "name" => q.OrderBy(s => s.FullName),
        "name_desc" => q.OrderByDescending(s => s.FullName),
        "no_desc" => q.OrderByDescending(s => s.StudentNo),
        "section" => q.OrderBy(s => s.Section).ThenBy(s => s.FullName),
        "debt" => q.OrderByDescending(s => s.Outstanding),
        "recent" => q.OrderByDescending(s => s.EnrollmentDate),
        _ => q.OrderBy(s => s.StudentNo)
    };

    private async Task FillFiltersAsync(StudentIndexViewModel vm)
    {
        var year = await GetCurrentYearAsync();

        vm.Stages = await _db.Stages.AsNoTracking().OrderBy(s => s.SeqNo)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString(), s.Id == vm.Filter.StageId))
            .ToListAsync();

        var gradeQuery = _db.Grades.AsNoTracking().AsQueryable();
        if (vm.Filter.StageId.HasValue) gradeQuery = gradeQuery.Where(g => g.StageId == vm.Filter.StageId);

        vm.Grades = await gradeQuery.OrderBy(g => g.SeqNo)
            .Select(g => new SelectListItem(g.Name, g.Id.ToString(), g.Id == vm.Filter.GradeId))
            .ToListAsync();

        var sectionQuery = _db.Sections.AsNoTracking().Where(s => s.IsActive);
        if (year is not null) sectionQuery = sectionQuery.Where(s => s.AcademicYearId == year.Id);
        if (vm.Filter.GradeId.HasValue) sectionQuery = sectionQuery.Where(s => s.GradeId == vm.Filter.GradeId);
        else if (vm.Filter.StageId.HasValue) sectionQuery = sectionQuery.Where(s => s.Grade.StageId == vm.Filter.StageId);

        vm.Sections = await sectionQuery
            .OrderBy(s => s.Grade.SeqNo).ThenBy(s => s.Name)
            .Select(s => new SelectListItem(s.Grade.Name + " - " + s.Name, s.Id.ToString(), s.Id == vm.Filter.SectionId))
            .ToListAsync();
    }

    // ==================================================================
    // التفاصيل
    // ==================================================================
    public async Task<IActionResult> Details(int id)
    {
        var student = await _db.Students
            .Include(s => s.CurrentSection).ThenInclude(sec => sec!.Grade).ThenInclude(g => g.Stage)
            .Include(s => s.CurrentSection).ThenInclude(sec => sec!.HomeroomTeacher)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student is null) return NotFound();

        var settings = await GetSettingsAsync();
        var vm = new StudentDetailsViewModel
        {
            Student = student,
            SectionName = student.CurrentSection is null ? null
                : $"{student.CurrentSection.Grade.Name} - {student.CurrentSection.Name}",
            GradeName = student.CurrentSection?.Grade.Name,
            StageName = student.CurrentSection?.Grade.Stage.Name,
            HomeroomTeacher = student.CurrentSection?.HomeroomTeacher?.FullName,
            Currency = settings.Currency
        };

        vm.HasAccount = await _db.Users.AnyAsync(u => u.StudentId == id);

        vm.Guardians = await _db.StudentGuardians
            .Where(sg => sg.StudentId == id)
            .Select(sg => new GuardianLinkRow
            {
                LinkId = sg.Id,
                GuardianId = sg.GuardianId,
                FullName = sg.Guardian.FullName,
                Phone = sg.Guardian.Phone,
                Email = sg.Guardian.Email,
                Relation = sg.Relation,
                IsPrimary = sg.IsPrimary,
                CanPickup = sg.CanPickup,
                Job = sg.Guardian.Job
            })
            .OrderByDescending(g => g.IsPrimary)
            .ToListAsync();

        // ---------- الحضور ----------
        var attStats = await _db.StudentAttendances
            .Where(a => a.StudentId == id)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        vm.Attendance = new AttendanceSummary
        {
            Present = attStats.FirstOrDefault(s => s.Status == AttendanceStatus.Present)?.Count ?? 0,
            Absent = attStats.FirstOrDefault(s => s.Status == AttendanceStatus.Absent)?.Count ?? 0,
            Late = attStats.FirstOrDefault(s => s.Status == AttendanceStatus.Late)?.Count ?? 0,
            Excused = attStats.FirstOrDefault(s => s.Status == AttendanceStatus.Excused)?.Count ?? 0,
            Recent = await _db.StudentAttendances
                .Where(a => a.StudentId == id)
                .OrderByDescending(a => a.Date)
                .Take(12)
                .ToListAsync()
        };

        // ---------- الدرجات ----------
        vm.Results = await _db.ExamResults
            .Where(r => r.StudentId == id)
            .OrderByDescending(r => r.Exam.ExamDate)
            .Take(20)
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

        // ---------- الملاحظات والمستندات ----------
        vm.Notes = await _db.StudentNotes
            .Include(n => n.Employee)
            .Where(n => n.StudentId == id)
            .OrderByDescending(n => n.NoteDate).ThenByDescending(n => n.Id)
            .ToListAsync();

        vm.Documents = await _db.StudentDocuments
            .Where(d => d.StudentId == id)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        vm.Transfers = await _db.StudentTransfers
            .Include(t => t.FromSection).ThenInclude(s => s!.Grade)
            .Include(t => t.ToSection).ThenInclude(s => s.Grade)
            .Where(t => t.StudentId == id)
            .OrderByDescending(t => t.TransferDate)
            .ToListAsync();

        // ---------- المالية ----------
        if (User.Can(Permissions.FinanceView))
        {
            var invoice = await _db.Invoices
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
        }

        // ---------- النقل ----------
        vm.Transport = await _db.StudentTransports
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

        vm.Sections = await SectionOptionsAsync(student.CurrentSectionId);
        return View(vm);
    }

    private async Task<List<SelectListItem>> SectionOptionsAsync(int? selected)
    {
        var year = await GetCurrentYearAsync();
        var q = _db.Sections.AsNoTracking().Where(s => s.IsActive);
        if (year is not null) q = q.Where(s => s.AcademicYearId == year.Id);

        return await q.OrderBy(s => s.Grade.SeqNo).ThenBy(s => s.Name)
            .Select(s => new SelectListItem(
                s.Grade.Name + " - " + s.Name,
                s.Id.ToString(),
                s.Id == selected))
            .ToListAsync();
    }

    // ==================================================================
    // إضافة وتعديل
    // ==================================================================
    [HasPermission(Permissions.StudentsCreate)]
    public async Task<IActionResult> Create()
    {
        var vm = new StudentFormViewModel
        {
            StudentNo = await NextStudentNoAsync(),
            Nationality = "عُماني",
            Religion = "الإسلام",
            Sections = await SectionOptionsAsync(null)
        };
        return View("Form", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.StudentsCreate)]
    public async Task<IActionResult> Create(StudentFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Sections = await SectionOptionsAsync(vm.CurrentSectionId);
            return View("Form", vm);
        }

        var year = await GetCurrentYearAsync();
        var studentNo = string.IsNullOrWhiteSpace(vm.StudentNo) ? await NextStudentNoAsync() : vm.StudentNo.Trim();

        if (await _db.Students.AnyAsync(s => s.StudentNo == studentNo))
        {
            ModelState.AddModelError(nameof(vm.StudentNo), "الرقم الطلابي مستخدم مسبقاً.");
            vm.Sections = await SectionOptionsAsync(vm.CurrentSectionId);
            return View("Form", vm);
        }

        var student = new Student
        {
            StudentNo = studentNo,
            FullName = vm.FullName.Trim(),
            FullNameEn = vm.FullNameEn,
            NationalId = vm.NationalId,
            Gender = vm.Gender,
            BirthDate = vm.BirthDate,
            BirthPlace = vm.BirthPlace,
            Nationality = vm.Nationality,
            Religion = vm.Religion,
            Address = vm.Address,
            Phone = vm.Phone,
            Email = vm.Email,
            EnrollmentDate = vm.EnrollmentDate,
            Status = vm.Status,
            CurrentSectionId = vm.CurrentSectionId,
            BloodType = vm.BloodType,
            HealthNotes = vm.HealthNotes,
            PreviousSchool = vm.PreviousSchool
        };

        try
        {
            student.PhotoPath = await _files.SaveAsync(vm.Photo, "students", IFileStorageService.ImageExtensions, 3 * 1024 * 1024);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(vm.Photo), ex.Message);
            vm.Sections = await SectionOptionsAsync(vm.CurrentSectionId);
            return View("Form", vm);
        }

        _db.Students.Add(student);
        await _db.SaveChangesAsync();

        // التسجيل في العام الدراسي الحالي
        if (year is not null && vm.CurrentSectionId.HasValue)
        {
            _db.Enrollments.Add(new Enrollment
            {
                StudentId = student.Id,
                SectionId = vm.CurrentSectionId.Value,
                AcademicYearId = year.Id,
                EnrollDate = vm.EnrollmentDate
            });
        }

        // ربط ولي الأمر
        if (vm.ExistingGuardianId.HasValue)
        {
            _db.StudentGuardians.Add(new StudentGuardian
            {
                StudentId = student.Id,
                GuardianId = vm.ExistingGuardianId.Value,
                Relation = vm.GuardianRelation ?? "الأب",
                IsPrimary = true
            });
        }
        else if (!string.IsNullOrWhiteSpace(vm.GuardianName) && !string.IsNullOrWhiteSpace(vm.GuardianPhone))
        {
            var guardian = new Guardian
            {
                FullName = vm.GuardianName.Trim(),
                Phone = vm.GuardianPhone.Trim()
            };
            _db.Guardians.Add(guardian);
            await _db.SaveChangesAsync();

            _db.StudentGuardians.Add(new StudentGuardian
            {
                StudentId = student.Id,
                GuardianId = guardian.Id,
                Relation = vm.GuardianRelation ?? "الأب",
                IsPrimary = true
            });
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("تسجيل طالب جديد", nameof(Student), student.Id, student.FullName);

        Success($"تم تسجيل الطالب «{student.FullName}» برقم {student.StudentNo} بنجاح.");
        return RedirectToAction(nameof(Details), new { id = student.Id });
    }

    [HasPermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> Edit(int id)
    {
        var s = await _db.Students.FindAsync(id);
        if (s is null) return NotFound();

        var vm = new StudentFormViewModel
        {
            Id = s.Id,
            StudentNo = s.StudentNo,
            FullName = s.FullName,
            FullNameEn = s.FullNameEn,
            NationalId = s.NationalId,
            Gender = s.Gender,
            BirthDate = s.BirthDate,
            BirthPlace = s.BirthPlace,
            Nationality = s.Nationality,
            Religion = s.Religion,
            Address = s.Address,
            Phone = s.Phone,
            Email = s.Email,
            EnrollmentDate = s.EnrollmentDate,
            Status = s.Status,
            CurrentSectionId = s.CurrentSectionId,
            BloodType = s.BloodType,
            HealthNotes = s.HealthNotes,
            PreviousSchool = s.PreviousSchool,
            PhotoPath = s.PhotoPath,
            Sections = await SectionOptionsAsync(s.CurrentSectionId)
        };

        return View("Form", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> Edit(StudentFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Sections = await SectionOptionsAsync(vm.CurrentSectionId);
            return View("Form", vm);
        }

        var s = await _db.Students.FindAsync(vm.Id);
        if (s is null) return NotFound();

        s.FullName = vm.FullName.Trim();
        s.FullNameEn = vm.FullNameEn;
        s.NationalId = vm.NationalId;
        s.Gender = vm.Gender;
        s.BirthDate = vm.BirthDate;
        s.BirthPlace = vm.BirthPlace;
        s.Nationality = vm.Nationality;
        s.Religion = vm.Religion;
        s.Address = vm.Address;
        s.Phone = vm.Phone;
        s.Email = vm.Email;
        s.EnrollmentDate = vm.EnrollmentDate;
        s.Status = vm.Status;
        s.CurrentSectionId = vm.CurrentSectionId;
        s.BloodType = vm.BloodType;
        s.HealthNotes = vm.HealthNotes;
        s.PreviousSchool = vm.PreviousSchool;

        if (vm.Photo is not null)
        {
            try
            {
                var path = await _files.SaveAsync(vm.Photo, "students", IFileStorageService.ImageExtensions, 3 * 1024 * 1024);
                if (path is not null)
                {
                    _files.Delete(s.PhotoPath);
                    s.PhotoPath = path;
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(vm.Photo), ex.Message);
                vm.PhotoPath = s.PhotoPath;
                vm.Sections = await SectionOptionsAsync(vm.CurrentSectionId);
                return View("Form", vm);
            }
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("تعديل بيانات طالب", nameof(Student), s.Id, s.FullName);

        Success("تم حفظ بيانات الطالب بنجاح.");
        return RedirectToAction(nameof(Details), new { id = s.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.StudentsDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await _db.Students.FindAsync(id);
        if (s is null) return NotFound();

        // منع الحذف عند وجود حركات مالية
        if (await _db.Payments.AnyAsync(p => p.StudentId == id))
        {
            Error("لا يمكن حذف الطالب لوجود حركات مالية مرتبطة به. يمكنك تغيير حالته إلى «منسحب» بدلاً من ذلك.");
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            _files.Delete(s.PhotoPath);
            _db.Students.Remove(s);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("حذف طالب", nameof(Student), id, s.FullName);
            Success("تم حذف الطالب بنجاح.");
        }
        catch (DbUpdateException)
        {
            Error("تعذّر حذف الطالب لارتباطه بسجلات أخرى (حضور، درجات، فواتير).");
            return RedirectToAction(nameof(Details), new { id });
        }

        return RedirectToAction(nameof(Index));
    }

    // ==================================================================
    // النقل بين الشعب
    // ==================================================================
    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.StudentsTransfer)]
    public async Task<IActionResult> Transfer(StudentTransferViewModel vm)
    {
        var student = await _db.Students.FindAsync(vm.StudentId);
        if (student is null) return NotFound();

        if (vm.ToSectionId == student.CurrentSectionId)
        {
            Warning("الطالب موجود بالفعل في هذه الشعبة.");
            return RedirectToAction(nameof(Details), new { id = vm.StudentId });
        }

        var year = await GetCurrentYearAsync();
        var from = student.CurrentSectionId;

        _db.StudentTransfers.Add(new StudentTransfer
        {
            StudentId = student.Id,
            FromSectionId = from,
            ToSectionId = vm.ToSectionId,
            TransferDate = vm.TransferDate,
            Reason = vm.Reason,
            PerformedByUserId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null
        });

        student.CurrentSectionId = vm.ToSectionId;

        // تحديث تسجيل العام الحالي
        if (year is not null)
        {
            var enrollment = await _db.Enrollments
                .FirstOrDefaultAsync(e => e.StudentId == student.Id && e.AcademicYearId == year.Id);

            if (enrollment is null)
                _db.Enrollments.Add(new Enrollment
                {
                    StudentId = student.Id,
                    SectionId = vm.ToSectionId,
                    AcademicYearId = year.Id,
                    EnrollDate = vm.TransferDate
                });
            else
                enrollment.SectionId = vm.ToSectionId;
        }

        await _db.SaveChangesAsync();

        var toName = await _db.Sections.Where(s => s.Id == vm.ToSectionId)
            .Select(s => s.Grade.Name + " - " + s.Name).FirstOrDefaultAsync();

        await _audit.LogAsync("نقل طالب بين الشعب", nameof(Student), student.Id, $"إلى {toName}");
        await _notify.NotifyGuardiansOfStudentAsync(student.Id, "نقل الطالب",
            $"تم نقل الطالب {student.FullName} إلى {toName}.", NotificationType.General, NotificationSeverity.Info);

        Success($"تم نقل الطالب إلى {toName}.");
        return RedirectToAction(nameof(Details), new { id = vm.StudentId });
    }

    // ==================================================================
    // المستندات
    // ==================================================================
    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.StudentsDocuments)]
    public async Task<IActionResult> UploadDocument(int studentId, string title, DocumentType docType, IFormFile file)
    {
        if (!await _db.Students.AnyAsync(s => s.Id == studentId)) return NotFound();

        if (file is null || file.Length == 0)
        {
            Error("الرجاء اختيار ملف للرفع.");
            return RedirectToAction(nameof(Details), new { id = studentId });
        }

        try
        {
            var path = await _files.SaveAsync(file, $"students/{studentId}", IFileStorageService.DocumentExtensions);
            _db.StudentDocuments.Add(new StudentDocument
            {
                StudentId = studentId,
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title,
                DocType = docType,
                FilePath = path!,
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                UploadedByUserId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null
            });
            await _db.SaveChangesAsync();
            Success("تم رفع المستند بنجاح.");
        }
        catch (InvalidOperationException ex)
        {
            Error(ex.Message);
        }

        return RedirectToAction(nameof(Details), new { id = studentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.StudentsDocuments)]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        var doc = await _db.StudentDocuments.FindAsync(id);
        if (doc is null) return NotFound();

        _files.Delete(doc.FilePath);
        _db.StudentDocuments.Remove(doc);
        await _db.SaveChangesAsync();

        Success("تم حذف المستند.");
        return RedirectToAction(nameof(Details), new { id = doc.StudentId });
    }

    // ==================================================================
    // الملاحظات والسلوك
    // ==================================================================
    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.StudentsNotes)]
    public async Task<IActionResult> AddNote(StudentNote note, bool notifyGuardian = false)
    {
        var student = await _db.Students.FindAsync(note.StudentId);
        if (student is null) return NotFound();

        if (string.IsNullOrWhiteSpace(note.Title))
        {
            Error("الرجاء إدخال عنوان الملاحظة.");
            return RedirectToAction(nameof(Details), new { id = note.StudentId });
        }

        note.EmployeeId = await HttpContext.RequestServices
            .GetRequiredService<ICurrentUserService>().GetEmployeeIdAsync();
        note.NotifyGuardian = notifyGuardian;

        _db.StudentNotes.Add(note);
        await _db.SaveChangesAsync();

        if (notifyGuardian)
        {
            var severity = note.Severity switch
            {
                NoteSeverity.Positive => NotificationSeverity.Success,
                NoteSeverity.Warning => NotificationSeverity.Warning,
                NoteSeverity.Violation or NoteSeverity.Severe => NotificationSeverity.Danger,
                _ => NotificationSeverity.Info
            };

            await _notify.NotifyGuardiansOfStudentAsync(note.StudentId,
                $"ملاحظة بخصوص {student.FullName}",
                $"{note.Title}. {note.Body}".Trim(),
                NotificationType.General, severity);
        }

        Success("تمت إضافة الملاحظة.");
        return RedirectToAction(nameof(Details), new { id = note.StudentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.StudentsNotes)]
    public async Task<IActionResult> DeleteNote(int id)
    {
        var note = await _db.StudentNotes.FindAsync(id);
        if (note is null) return NotFound();

        _db.StudentNotes.Remove(note);
        await _db.SaveChangesAsync();

        Success("تم حذف الملاحظة.");
        return RedirectToAction(nameof(Details), new { id = note.StudentId });
    }

    // ==================================================================
    // ربط أولياء الأمور
    // ==================================================================
    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> LinkGuardian(int studentId, int guardianId, string relation, bool isPrimary)
    {
        if (await _db.StudentGuardians.AnyAsync(sg => sg.StudentId == studentId && sg.GuardianId == guardianId))
        {
            Warning("ولي الأمر مرتبط بالطالب مسبقاً.");
            return RedirectToAction(nameof(Details), new { id = studentId });
        }

        if (isPrimary)
        {
            var others = await _db.StudentGuardians.Where(sg => sg.StudentId == studentId).ToListAsync();
            foreach (var o in others) o.IsPrimary = false;
        }

        _db.StudentGuardians.Add(new StudentGuardian
        {
            StudentId = studentId,
            GuardianId = guardianId,
            Relation = string.IsNullOrWhiteSpace(relation) ? "ولي أمر" : relation,
            IsPrimary = isPrimary
        });

        await _db.SaveChangesAsync();
        Success("تم ربط ولي الأمر بالطالب.");
        return RedirectToAction(nameof(Details), new { id = studentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [HasPermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> UnlinkGuardian(int linkId)
    {
        var link = await _db.StudentGuardians.FindAsync(linkId);
        if (link is null) return NotFound();

        _db.StudentGuardians.Remove(link);
        await _db.SaveChangesAsync();

        Success("تم إلغاء الربط.");
        return RedirectToAction(nameof(Details), new { id = link.StudentId });
    }

    // ==================================================================
    // بطاقة الطالب و QR
    // ==================================================================
    public async Task<IActionResult> IdCard(int id)
    {
        var student = await _db.Students
            .Include(s => s.CurrentSection).ThenInclude(sec => sec!.Grade)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student is null) return NotFound();

        ViewBag.Settings = await GetSettingsAsync();
        ViewBag.QrDataUrl = _qr.GenerateDataUrl($"STU:{student.QrToken}", 6);
        return View(student);
    }

    public async Task<IActionResult> QrImage(int id)
    {
        var token = await _db.Students.Where(s => s.Id == id).Select(s => s.QrToken).FirstOrDefaultAsync();
        if (token is null) return NotFound();

        return File(_qr.GeneratePng($"STU:{token}", 10), "image/png");
    }

    // ==================================================================
    // التصدير
    // ==================================================================
    [HasPermission(Permissions.ReportsExport)]
    public async Task<IActionResult> Export(StudentFilter filter, string format = "excel")
    {
        filter.PageSize = 5000;
        filter.Page = 1;

        var rows = await ApplySort(BuildQuery(filter), filter.Sort).Take(5000).ToListAsync();
        var settings = await GetSettingsAsync();

        var columns = new List<ExportColumn>
        {
            new("الرقم الطلابي", 1f), new("اسم الطالب", 2.4f), new("الجنس", .8f),
            new("الصف/الشعبة", 1.4f), new("المرحلة", 1.2f), new("ولي الأمر", 2f),
            new("جوال ولي الأمر", 1.2f), new("الحالة", 1f), new("المتبقي", 1f)
        };

        var data = rows.Select(r => new string?[]
        {
            r.StudentNo, r.FullName, r.Gender.Display(), r.Section, r.Stage,
            r.GuardianName, r.GuardianPhone, r.Status.Display(), r.Outstanding.ToString("N2")
        });

        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
        {
            var pdf = _export.ToPdf("كشف الطلاب", $"عدد السجلات: {rows.Count}", columns, data,
                settings.SchoolName, logoPath: settings.LogoPath);
            return File(pdf, "application/pdf", $"students-{DateTime.Now:yyyyMMdd-HHmm}.pdf");
        }

        var xlsx = _export.ToExcel("كشف الطلاب", columns, data);
        return File(xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"students-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }

    // ==================================================================
    private async Task<string> NextStudentNoAsync()
    {
        var year = await GetCurrentYearAsync();
        var prefix = (year?.StartDate.Year ?? DateTime.Today.Year).ToString();

        var last = await _db.Students
            .Where(s => s.StudentNo.StartsWith(prefix))
            .OrderByDescending(s => s.StudentNo)
            .Select(s => s.StudentNo)
            .FirstOrDefaultAsync();

        var n = 1;
        if (last is not null && last.Length > prefix.Length &&
            int.TryParse(last[prefix.Length..], out var parsed))
            n = parsed + 1;

        return prefix + n.ToString("D4");
    }
}
