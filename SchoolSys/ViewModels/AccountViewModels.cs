using System.ComponentModel.DataAnnotations;

namespace SchoolSys.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "الرجاء إدخال البريد الإلكتروني")]
    [Display(Name = "البريد الإلكتروني")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "الرجاء إدخال كلمة المرور")]
    [DataType(DataType.Password), Display(Name = "كلمة المرور")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "تذكرني")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "الرجاء إدخال كلمة المرور الحالية")]
    [DataType(DataType.Password), Display(Name = "كلمة المرور الحالية")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "الرجاء إدخال كلمة المرور الجديدة")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور يجب ألا تقل عن 6 أحرف")]
    [DataType(DataType.Password), Display(Name = "كلمة المرور الجديدة")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password), Display(Name = "تأكيد كلمة المرور")]
    [Compare(nameof(NewPassword), ErrorMessage = "كلمتا المرور غير متطابقتين")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ProfileViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "الرجاء إدخال الاسم")]
    [Display(Name = "الاسم الكامل")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "البريد الإلكتروني")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "رقم الجوال")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "الصورة الشخصية")]
    public string? PhotoPath { get; set; }

    public IFormFile? Photo { get; set; }

    public List<string> Roles { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
