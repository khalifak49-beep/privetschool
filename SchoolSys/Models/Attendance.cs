using System.ComponentModel.DataAnnotations;

namespace SchoolSys.Models;

/// <summary>حضور وانصراف الطلاب</summary>
public class StudentAttendance
{
    public int Id { get; set; }

    [Display(Name = "الطالب")]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Display(Name = "الشعبة")]
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    [Display(Name = "العام الدراسي")]
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    [Display(Name = "التاريخ"), DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [Display(Name = "الحالة")]
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    [Display(Name = "وقت الحضور")]
    public TimeSpan? CheckInTime { get; set; }

    [Display(Name = "وقت الانصراف")]
    public TimeSpan? CheckOutTime { get; set; }

    [Display(Name = "دقائق التأخير")]
    public int LateMinutes { get; set; }

    [Display(Name = "طريقة التسجيل")]
    public AttendanceMethod Method { get; set; } = AttendanceMethod.Manual;

    [StringLength(400), Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    [Display(Name = "تم إشعار ولي الأمر")]
    public bool GuardianNotified { get; set; }

    public int? RecordedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>حضور وانصراف الموظفين والمعلمين</summary>
public class StaffAttendance
{
    public int Id { get; set; }

    [Display(Name = "الموظف")]
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    [Display(Name = "التاريخ"), DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [Display(Name = "الحالة")]
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    [Display(Name = "وقت الحضور")]
    public TimeSpan? CheckInTime { get; set; }

    [Display(Name = "وقت الانصراف")]
    public TimeSpan? CheckOutTime { get; set; }

    [Display(Name = "دقائق التأخير")]
    public int LateMinutes { get; set; }

    [Display(Name = "طريقة التسجيل")]
    public AttendanceMethod Method { get; set; } = AttendanceMethod.Manual;

    [StringLength(400), Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    public int? RecordedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
