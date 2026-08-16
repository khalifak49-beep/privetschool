using System.ComponentModel.DataAnnotations;

namespace SchoolSys.Models;

public enum Gender
{
    [Display(Name = "ذكر")] Male = 1,
    [Display(Name = "أنثى")] Female = 2
}

public enum StudentStatus
{
    [Display(Name = "نشط")] Active = 1,
    [Display(Name = "منقول")] Transferred = 2,
    [Display(Name = "متخرج")] Graduated = 3,
    [Display(Name = "منسحب")] Withdrawn = 4,
    [Display(Name = "موقوف")] Suspended = 5
}

public enum EmployeeType
{
    [Display(Name = "معلم")] Teacher = 1,
    [Display(Name = "إداري")] Admin = 2,
    [Display(Name = "محاسب")] Accountant = 3,
    [Display(Name = "سائق")] Driver = 4,
    [Display(Name = "مشرف حافلة")] BusSupervisor = 5,
    [Display(Name = "موظف استقبال")] Receptionist = 6,
    [Display(Name = "أخرى")] Other = 7
}

public enum AttendanceStatus
{
    [Display(Name = "حاضر")] Present = 1,
    [Display(Name = "غائب")] Absent = 2,
    [Display(Name = "متأخر")] Late = 3,
    [Display(Name = "غياب بعذر")] Excused = 4,
    [Display(Name = "إذن انصراف")] EarlyLeave = 5
}

public enum AttendanceMethod
{
    [Display(Name = "يدوي")] Manual = 1,
    [Display(Name = "QR Code")] QrCode = 2,
    [Display(Name = "استيراد")] Import = 3
}

public enum NoteType
{
    [Display(Name = "سلوك")] Behavior = 1,
    [Display(Name = "أكاديمي")] Academic = 2,
    [Display(Name = "صحي")] Health = 3,
    [Display(Name = "عام")] General = 4,
    [Display(Name = "تميّز")] Excellence = 5
}

public enum NoteSeverity
{
    [Display(Name = "إيجابي")] Positive = 1,
    [Display(Name = "معلومة")] Info = 2,
    [Display(Name = "تنبيه")] Warning = 3,
    [Display(Name = "مخالفة")] Violation = 4,
    [Display(Name = "مخالفة جسيمة")] Severe = 5
}

public enum ExamType
{
    [Display(Name = "اختبار قصير")] Quiz = 1,
    [Display(Name = "شهري")] Monthly = 2,
    [Display(Name = "نصف الفصل")] Midterm = 3,
    [Display(Name = "نهائي")] Final = 4,
    [Display(Name = "عملي")] Practical = 5,
    [Display(Name = "أعمال السنة")] Coursework = 6
}

public enum ExamStatus
{
    [Display(Name = "مسودة")] Draft = 1,
    [Display(Name = "معلن")] Published = 2,
    [Display(Name = "مرصود")] Graded = 3,
    [Display(Name = "معتمد")] Approved = 4
}

public enum HomeworkStatus
{
    [Display(Name = "لم يسلّم")] NotSubmitted = 1,
    [Display(Name = "مسلّم")] Submitted = 2,
    [Display(Name = "متأخر")] Late = 3,
    [Display(Name = "مصحح")] Graded = 4
}

public enum InvoiceStatus
{
    [Display(Name = "غير مدفوعة")] Unpaid = 1,
    [Display(Name = "مدفوعة جزئياً")] PartiallyPaid = 2,
    [Display(Name = "مدفوعة")] Paid = 3,
    [Display(Name = "ملغاة")] Cancelled = 4
}

public enum InstallmentStatus
{
    [Display(Name = "مستحق لاحقاً")] Pending = 1,
    [Display(Name = "مدفوع جزئياً")] Partial = 2,
    [Display(Name = "مدفوع")] Paid = 3,
    [Display(Name = "متأخر")] Overdue = 4
}

public enum PaymentMethod
{
    [Display(Name = "نقداً")] Cash = 1,
    [Display(Name = "تحويل بنكي")] BankTransfer = 2,
    [Display(Name = "بطاقة")] Card = 3,
    [Display(Name = "شيك")] Cheque = 4,
    [Display(Name = "دفع إلكتروني")] Online = 5
}

public enum DiscountType
{
    [Display(Name = "نسبة مئوية")] Percentage = 1,
    [Display(Name = "مبلغ ثابت")] FixedAmount = 2
}

public enum TransportDirection
{
    [Display(Name = "ذهاب إلى المدرسة")] ToSchool = 1,
    [Display(Name = "عودة إلى المنزل")] ToHome = 2
}

public enum TransportEvent
{
    [Display(Name = "صعود")] Board = 1,
    [Display(Name = "نزول")] Alight = 2
}

public enum AnnouncementAudience
{
    [Display(Name = "الجميع")] All = 1,
    [Display(Name = "المعلمون")] Teachers = 2,
    [Display(Name = "الطلاب")] Students = 3,
    [Display(Name = "أولياء الأمور")] Guardians = 4,
    [Display(Name = "الموظفون")] Staff = 5
}

public enum NotificationType
{
    [Display(Name = "عام")] General = 1,
    [Display(Name = "غياب")] Attendance = 2,
    [Display(Name = "درجات")] Grades = 3,
    [Display(Name = "رسوم")] Fees = 4,
    [Display(Name = "واجب")] Homework = 5,
    [Display(Name = "نقل")] Transport = 6,
    [Display(Name = "رسالة")] Message = 7,
    [Display(Name = "إعلان")] Announcement = 8
}

public enum NotificationSeverity
{
    [Display(Name = "معلومة")] Info = 1,
    [Display(Name = "نجاح")] Success = 2,
    [Display(Name = "تحذير")] Warning = 3,
    [Display(Name = "خطر")] Danger = 4
}

public enum OutboxChannel
{
    [Display(Name = "رسالة نصية")] Sms = 1,
    [Display(Name = "واتساب")] WhatsApp = 2,
    [Display(Name = "بريد إلكتروني")] Email = 3
}

public enum OutboxStatus
{
    [Display(Name = "في الانتظار")] Queued = 1,
    [Display(Name = "تم الإرسال")] Sent = 2,
    [Display(Name = "فشل")] Failed = 3,
    [Display(Name = "ملغى")] Cancelled = 4
}

public enum DocumentType
{
    [Display(Name = "شهادة ميلاد")] BirthCertificate = 1,
    [Display(Name = "هوية / جواز")] Identity = 2,
    [Display(Name = "شهادة دراسية")] AcademicCertificate = 3,
    [Display(Name = "تقرير طبي")] Medical = 4,
    [Display(Name = "صورة شخصية")] Photo = 5,
    [Display(Name = "أخرى")] Other = 6
}
