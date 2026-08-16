using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolSys.Models;
using System.ComponentModel.DataAnnotations;

namespace SchoolSys.ViewModels;

public class StudentFilter
{
    public string? Q { get; set; }
    public int? StageId { get; set; }
    public int? GradeId { get; set; }
    public int? SectionId { get; set; }
    public StudentStatus? Status { get; set; }
    public Gender? Gender { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Sort { get; set; }
}

public class StudentListRow
{
    public int Id { get; set; }
    public string StudentNo { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? PhotoPath { get; set; }
    public Gender Gender { get; set; }
    public string? Section { get; set; }
    public string? Stage { get; set; }
    public StudentStatus Status { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public decimal Outstanding { get; set; }
    public DateTime EnrollmentDate { get; set; }
}

public class StudentIndexViewModel
{
    public StudentFilter Filter { get; set; } = new();
    public PagedList<StudentListRow> Students { get; set; } = PagedList<StudentListRow>.Empty();
    public List<SelectListItem> Stages { get; set; } = [];
    public List<SelectListItem> Grades { get; set; } = [];
    public List<SelectListItem> Sections { get; set; } = [];
    public int ActiveCount { get; set; }
    public int MaleCount { get; set; }
    public int FemaleCount { get; set; }
}

public class StudentFormViewModel
{
    public int Id { get; set; }

    [Display(Name = "الرقم الطلابي")]
    public string? StudentNo { get; set; }

    [Required(ErrorMessage = "الرجاء إدخال اسم الطالب")]
    [StringLength(150), Display(Name = "اسم الطالب (رباعي)")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(150), Display(Name = "الاسم بالإنجليزية")]
    public string? FullNameEn { get; set; }

    [StringLength(30), Display(Name = "رقم الهوية / جواز السفر")]
    public string? NationalId { get; set; }

    [Display(Name = "الجنس")]
    public Gender Gender { get; set; } = Models.Gender.Male;

    [Display(Name = "تاريخ الميلاد"), DataType(DataType.Date)]
    public DateTime? BirthDate { get; set; }

    [StringLength(100), Display(Name = "مكان الميلاد")]
    public string? BirthPlace { get; set; }

    [StringLength(60), Display(Name = "الجنسية")]
    public string? Nationality { get; set; }

    [StringLength(60), Display(Name = "الديانة")]
    public string? Religion { get; set; }

    [StringLength(300), Display(Name = "العنوان")]
    public string? Address { get; set; }

    [StringLength(30), Display(Name = "هاتف الطالب")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
    [StringLength(150), Display(Name = "البريد الإلكتروني")]
    public string? Email { get; set; }

    [Display(Name = "تاريخ الالتحاق"), DataType(DataType.Date)]
    public DateTime EnrollmentDate { get; set; } = DateTime.Today;

    [Display(Name = "الحالة")]
    public StudentStatus Status { get; set; } = StudentStatus.Active;

    [Required(ErrorMessage = "الرجاء اختيار الشعبة")]
    [Display(Name = "الشعبة")]
    public int? CurrentSectionId { get; set; }

    [StringLength(10), Display(Name = "فصيلة الدم")]
    public string? BloodType { get; set; }

    [StringLength(500), Display(Name = "ملاحظات صحية")]
    public string? HealthNotes { get; set; }

    [StringLength(150), Display(Name = "المدرسة السابقة")]
    public string? PreviousSchool { get; set; }

    [Display(Name = "الصورة الشخصية")]
    public IFormFile? Photo { get; set; }
    public string? PhotoPath { get; set; }

    // ولي أمر يُنشأ مع الطالب (اختياري عند الإضافة)
    [Display(Name = "اسم ولي الأمر")]
    [StringLength(150)]
    public string? GuardianName { get; set; }

    [Display(Name = "جوال ولي الأمر")]
    [StringLength(30)]
    public string? GuardianPhone { get; set; }

    [Display(Name = "صلة القرابة")]
    [StringLength(50)]
    public string? GuardianRelation { get; set; } = "الأب";

    [Display(Name = "ربط بولي أمر مسجّل")]
    public int? ExistingGuardianId { get; set; }

    public List<SelectListItem> Sections { get; set; } = [];
    public bool IsEdit => Id > 0;
}

public class StudentDetailsViewModel
{
    public Student Student { get; set; } = null!;
    public string? SectionName { get; set; }
    public string? StageName { get; set; }
    public string? GradeName { get; set; }
    public string? HomeroomTeacher { get; set; }
    public List<GuardianLinkRow> Guardians { get; set; } = [];
    public AttendanceSummary Attendance { get; set; } = new();
    public List<StudentSubjectResultRow> Results { get; set; } = [];
    public List<StudentNote> Notes { get; set; } = [];
    public List<StudentDocument> Documents { get; set; } = [];
    public List<StudentTransfer> Transfers { get; set; } = [];
    public StudentFinanceSummary Finance { get; set; } = new();
    public TransportInfo? Transport { get; set; }
    public List<SelectListItem> Sections { get; set; } = [];
    public string Currency { get; set; } = "ر.ع";
    public bool HasAccount { get; set; }
}

public class GuardianLinkRow
{
    public int LinkId { get; set; }
    public int GuardianId { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string Relation { get; set; } = "";
    public bool IsPrimary { get; set; }
    public bool CanPickup { get; set; }
    public string? Job { get; set; }
}

public class AttendanceSummary
{
    public int Present { get; set; }
    public int Absent { get; set; }
    public int Late { get; set; }
    public int Excused { get; set; }
    public int Total => Present + Absent + Late + Excused;
    public double Rate => Total > 0 ? Math.Round((double)(Present + Late) / Total * 100, 1) : 0;
    public List<StudentAttendance> Recent { get; set; } = [];
}

public class StudentSubjectResultRow
{
    public string Subject { get; set; } = "";
    public string ExamTitle { get; set; } = "";
    public ExamType ExamType { get; set; }
    public DateTime ExamDate { get; set; }
    public decimal? Score { get; set; }
    public decimal MaxScore { get; set; }
    public bool IsAbsent { get; set; }
    public decimal Percentage => MaxScore > 0 && Score.HasValue ? Math.Round(Score.Value / MaxScore * 100m, 1) : 0;
}

public class StudentFinanceSummary
{
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    public decimal Remaining => Total - Paid;
    public decimal Overdue { get; set; }
    public List<Installment> Installments { get; set; } = [];
    public int? InvoiceId { get; set; }
    public string? InvoiceNo { get; set; }
}

public class TransportInfo
{
    public string RouteName { get; set; } = "";
    public string? StopName { get; set; }
    public string? BusNo { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public decimal MonthlyFee { get; set; }
}

public class StudentTransferViewModel
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = "";
    public int? CurrentSectionId { get; set; }
    public string? CurrentSectionName { get; set; }

    [Required(ErrorMessage = "الرجاء اختيار الشعبة الجديدة")]
    [Display(Name = "الشعبة الجديدة")]
    public int ToSectionId { get; set; }

    [Display(Name = "تاريخ النقل"), DataType(DataType.Date)]
    public DateTime TransferDate { get; set; } = DateTime.Today;

    [StringLength(400), Display(Name = "سبب النقل")]
    public string? Reason { get; set; }

    public List<SelectListItem> Sections { get; set; } = [];
}
