using SchoolSys.Models;
using System.ComponentModel.DataAnnotations;

namespace SchoolSys.ViewModels;

// ===================== أولياء الأمور =====================
public class GuardianListRow
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string? Job { get; set; }
    public int ChildrenCount { get; set; }
    public string? Children { get; set; }
    public bool HasAccount { get; set; }
    public bool IsActive { get; set; }
    public decimal Outstanding { get; set; }
}

public class GuardianIndexViewModel
{
    public string? Q { get; set; }
    public bool? HasAccount { get; set; }
    public int Page { get; set; } = 1;
    public PagedList<GuardianListRow> Guardians { get; set; } = PagedList<GuardianListRow>.Empty();
    public int TotalGuardians { get; set; }
    public int WithAccounts { get; set; }
}

public class GuardianFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "الرجاء إدخال الاسم")]
    [StringLength(150), Display(Name = "اسم ولي الأمر")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(30), Display(Name = "رقم الهوية")]
    public string? NationalId { get; set; }

    [Required(ErrorMessage = "الرجاء إدخال رقم الجوال")]
    [StringLength(30), Display(Name = "رقم الجوال")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(30), Display(Name = "هاتف بديل")]
    public string? AltPhone { get; set; }

    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
    [StringLength(150), Display(Name = "البريد الإلكتروني")]
    public string? Email { get; set; }

    [StringLength(100), Display(Name = "المهنة")]
    public string? Job { get; set; }

    [StringLength(150), Display(Name = "جهة العمل")]
    public string? Workplace { get; set; }

    [StringLength(300), Display(Name = "العنوان")]
    public string? Address { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    public bool IsEdit => Id > 0;
}

public class GuardianDetailsViewModel
{
    public Guardian Guardian { get; set; } = null!;
    public List<GuardianChildRow> Children { get; set; } = [];
    public bool HasAccount { get; set; }
    public string? AccountEmail { get; set; }
    public string Currency { get; set; } = "ر.ع";
}

public class GuardianChildRow
{
    public int StudentId { get; set; }
    public int LinkId { get; set; }
    public string StudentNo { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? PhotoPath { get; set; }
    public string? Section { get; set; }
    public string Relation { get; set; } = "";
    public bool IsPrimary { get; set; }
    public StudentStatus Status { get; set; }
    public double AttendanceRate { get; set; }
    public decimal Outstanding { get; set; }
}

// ===================== الموظفون =====================
public class EmployeeListRow
{
    public int Id { get; set; }
    public string EmployeeNo { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? PhotoPath { get; set; }
    public EmployeeType EmployeeType { get; set; }
    public string? Specialization { get; set; }
    public string? Phone { get; set; }
    public DateTime HireDate { get; set; }
    public bool IsActive { get; set; }
    public bool HasAccount { get; set; }
    public int SectionsCount { get; set; }
}

public class EmployeeIndexViewModel
{
    public string? Q { get; set; }
    public EmployeeType? Type { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public PagedList<EmployeeListRow> Employees { get; set; } = PagedList<EmployeeListRow>.Empty();
    public int TotalCount { get; set; }
    public int TeachersCount { get; set; }
    public int ActiveCount { get; set; }
}

public class EmployeeFormViewModel
{
    public int Id { get; set; }

    [Display(Name = "الرقم الوظيفي")]
    public string? EmployeeNo { get; set; }

    [Required(ErrorMessage = "الرجاء إدخال الاسم")]
    [StringLength(150), Display(Name = "الاسم الكامل")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "الوظيفة")]
    public EmployeeType EmployeeType { get; set; } = EmployeeType.Teacher;

    [StringLength(30), Display(Name = "رقم الهوية")]
    public string? NationalId { get; set; }

    [Display(Name = "الجنس")]
    public Gender Gender { get; set; } = Gender.Male;

    [Display(Name = "تاريخ الميلاد"), DataType(DataType.Date)]
    public DateTime? BirthDate { get; set; }

    [StringLength(30), Display(Name = "الجوال")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
    [StringLength(150), Display(Name = "البريد الإلكتروني")]
    public string? Email { get; set; }

    [StringLength(300), Display(Name = "العنوان")]
    public string? Address { get; set; }

    [Display(Name = "تاريخ التعيين"), DataType(DataType.Date)]
    public DateTime HireDate { get; set; } = DateTime.Today;

    [StringLength(150), Display(Name = "التخصص")]
    public string? Specialization { get; set; }

    [StringLength(150), Display(Name = "المؤهل العلمي")]
    public string? Qualification { get; set; }

    [Display(Name = "الراتب")]
    public decimal? Salary { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "الصورة الشخصية")]
    public IFormFile? Photo { get; set; }
    public string? PhotoPath { get; set; }

    public bool IsEdit => Id > 0;
}

public class EmployeeDetailsViewModel
{
    public Employee Employee { get; set; } = null!;
    public List<TeachingLoadRow> TeachingLoad { get; set; } = [];
    public List<TodayLessonRow> Timetable { get; set; } = [];
    public List<Section> HomeroomSections { get; set; } = [];
    public bool HasAccount { get; set; }
    public string? AccountEmail { get; set; }
    public int AttendancePresent { get; set; }
    public int AttendanceAbsent { get; set; }
    public int AttendanceLate { get; set; }
    public string Currency { get; set; } = "ر.ع";
}

public class TeachingLoadRow
{
    public int Id { get; set; }
    public string Subject { get; set; } = "";
    public string Section { get; set; } = "";
    public int SectionId { get; set; }
    public int Students { get; set; }
}

/// <summary>إنشاء حساب دخول لشخص (طالب / ولي أمر / موظف).</summary>
public class CreateAccountViewModel
{
    public int EntityId { get; set; }
    public string EntityName { get; set; } = "";
    public string EntityType { get; set; } = "";

    [Required(ErrorMessage = "الرجاء إدخال البريد الإلكتروني")]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
    [Display(Name = "البريد الإلكتروني")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "الرجاء إدخال كلمة المرور")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور يجب ألا تقل عن 6 أحرف")]
    [DataType(DataType.Password), Display(Name = "كلمة المرور")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "الدور")]
    public string Role { get; set; } = "";
}
