using SchoolSys.Models;
using SchoolSys.Security;
using System.ComponentModel.DataAnnotations;

namespace SchoolSys.ViewModels;

public class UserIndexViewModel
{
    public string? Q { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public PagedList<UserRow> Users { get; set; } = PagedList<UserRow>.Empty();
    public List<string> Roles { get; set; } = [];
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
}

public class UserRow
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string? PhotoPath { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<string> Roles { get; set; } = [];
    public string? LinkedTo { get; set; }
    public bool IsLockedOut { get; set; }
}

public class UserFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "الرجاء إدخال الاسم")]
    [StringLength(150), Display(Name = "الاسم الكامل")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "الرجاء إدخال البريد الإلكتروني")]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
    [Display(Name = "البريد الإلكتروني")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "رقم الجوال")]
    public string? PhoneNumber { get; set; }

    [DataType(DataType.Password), Display(Name = "كلمة المرور")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور يجب ألا تقل عن 6 أحرف")]
    public string? Password { get; set; }

    [Display(Name = "الأدوار")]
    public List<string> SelectedRoles { get; set; } = [];

    [Display(Name = "الحساب نشط")]
    public bool IsActive { get; set; } = true;

    public List<string> AllRoles { get; set; } = [];
    public bool IsEdit => Id > 0;
    public string? LinkedTo { get; set; }
}

public class RoleIndexViewModel
{
    public List<RoleRow> Roles { get; set; } = [];
}

public class RoleRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public int UsersCount { get; set; }
    public int PermissionsCount { get; set; }
}

public class RolePermissionsViewModel
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public int UsersCount { get; set; }
    public HashSet<string> Granted { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<PermissionGroup> Groups => Permissions.Groups;
    public int TotalPermissions => Permissions.AllPermissions.Count();
}

public class AuditIndexViewModel
{
    public string? Q { get; set; }
    public string? Action { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public PagedList<AuditLog> Logs { get; set; } = PagedList<AuditLog>.Empty();
    public List<string> Actions { get; set; } = [];
}

public class SettingsViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "الرجاء إدخال اسم المدرسة")]
    [StringLength(200), Display(Name = "اسم المدرسة")]
    public string SchoolName { get; set; } = string.Empty;

    [StringLength(200), Display(Name = "الاسم بالإنجليزية")]
    public string? SchoolNameEn { get; set; }

    [StringLength(300), Display(Name = "العنوان")]
    public string? Address { get; set; }

    [StringLength(30), Display(Name = "الهاتف")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
    [StringLength(150), Display(Name = "البريد الإلكتروني")]
    public string? Email { get; set; }

    [StringLength(150), Display(Name = "الموقع الإلكتروني")]
    public string? Website { get; set; }

    [Required, StringLength(10), Display(Name = "رمز العملة")]
    public string Currency { get; set; } = "ر.ع";

    [Display(Name = "بداية الدوام")]
    public TimeSpan SchoolStartTime { get; set; }

    [Display(Name = "نهاية الدوام")]
    public TimeSpan SchoolEndTime { get; set; }

    [Range(0, 120), Display(Name = "دقائق السماح قبل احتساب التأخير")]
    public int LateGraceMinutes { get; set; } = 10;

    [Display(Name = "إشعار ولي الأمر تلقائياً عند غياب الطالب")]
    public bool AutoNotifyGuardianOnAbsence { get; set; }

    [Display(Name = "تفعيل الرسائل النصية (SMS)")]
    public bool EnableSms { get; set; }

    [Display(Name = "تفعيل واتساب")]
    public bool EnableWhatsApp { get; set; }

    [Display(Name = "شعار المدرسة")]
    public IFormFile? Logo { get; set; }
    public string? LogoPath { get; set; }
}

public class ReportsCenterViewModel
{
    public int Students { get; set; }
    public int Employees { get; set; }
    public int Sections { get; set; }
    public int Invoices { get; set; }
    public int AttendanceRecords { get; set; }
    public int ExamResults { get; set; }
    public string Currency { get; set; } = "ر.ع";
    public string? YearName { get; set; }
}
