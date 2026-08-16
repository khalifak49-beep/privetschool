using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace SchoolSys.Models;

public class ApplicationUser : IdentityUser<int>
{
    [Required, StringLength(150), Display(Name = "الاسم الكامل")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(300), Display(Name = "الصورة الشخصية")]
    public string? PhotoPath { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "تاريخ الإنشاء")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Display(Name = "آخر دخول")]
    public DateTime? LastLoginAt { get; set; }

    /// <summary>ربط الحساب بسجل الطالب / ولي الأمر / الموظف حسب الدور.</summary>
    public int? StudentId { get; set; }
    public Student? Student { get; set; }

    public int? GuardianId { get; set; }
    public Guardian? Guardian { get; set; }

    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
}

public class ApplicationRole : IdentityRole<int>
{
    [StringLength(150), Display(Name = "الاسم المعروض")]
    public string? DisplayName { get; set; }

    [StringLength(400), Display(Name = "الوصف")]
    public string? Description { get; set; }

    /// <summary>أدوار النظام الأساسية لا يمكن حذفها.</summary>
    public bool IsSystemRole { get; set; }
}

public class ApplicationUserRole : IdentityUserRole<int>
{
    public ApplicationUser User { get; set; } = null!;
    public ApplicationRole Role { get; set; } = null!;
}
