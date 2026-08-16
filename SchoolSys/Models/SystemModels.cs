using System.ComponentModel.DataAnnotations;

namespace SchoolSys.Models;

/// <summary>إعدادات المدرسة (سجل واحد)</summary>
public class SchoolSetting
{
    public int Id { get; set; }

    [Required, StringLength(200), Display(Name = "اسم المدرسة")]
    public string SchoolName { get; set; } = "المدرسة الأهلية النموذجية";

    [StringLength(200), Display(Name = "الاسم بالإنجليزية")]
    public string? SchoolNameEn { get; set; }

    [StringLength(300), Display(Name = "شعار المدرسة")]
    public string? LogoPath { get; set; }

    [StringLength(300), Display(Name = "العنوان")]
    public string? Address { get; set; }

    [StringLength(30), Display(Name = "الهاتف")]
    public string? Phone { get; set; }

    [StringLength(150), Display(Name = "البريد الإلكتروني")]
    public string? Email { get; set; }

    [StringLength(150), Display(Name = "الموقع الإلكتروني")]
    public string? Website { get; set; }

    [StringLength(10), Display(Name = "العملة")]
    public string Currency { get; set; } = "ر.ع";

    [Display(Name = "بداية الدوام")]
    public TimeSpan SchoolStartTime { get; set; } = new(7, 15, 0);

    [Display(Name = "نهاية الدوام")]
    public TimeSpan SchoolEndTime { get; set; } = new(13, 30, 0);

    /// <summary>دقائق السماح قبل احتساب التأخير.</summary>
    [Display(Name = "دقائق السماح")]
    public int LateGraceMinutes { get; set; } = 10;

    [Display(Name = "إشعار ولي الأمر عند الغياب تلقائياً")]
    public bool AutoNotifyGuardianOnAbsence { get; set; } = true;

    [Display(Name = "تفعيل الرسائل النصية")]
    public bool EnableSms { get; set; }

    [Display(Name = "تفعيل واتساب")]
    public bool EnableWhatsApp { get; set; }
}

/// <summary>سجل تدقيق العمليات</summary>
public class AuditLog
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    [StringLength(150), Display(Name = "المستخدم")]
    public string? UserName { get; set; }

    [Required, StringLength(60), Display(Name = "العملية")]
    public string Action { get; set; } = string.Empty;

    [StringLength(100), Display(Name = "الكيان")]
    public string? EntityName { get; set; }

    [StringLength(60)]
    public string? EntityId { get; set; }

    [StringLength(2000), Display(Name = "التفاصيل")]
    public string? Details { get; set; }

    [StringLength(60), Display(Name = "عنوان IP")]
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
