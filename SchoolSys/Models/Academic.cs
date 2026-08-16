using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolSys.Models;

/// <summary>العام الدراسي</summary>
public class AcademicYear
{
    public int Id { get; set; }

    [Required, StringLength(50), Display(Name = "العام الدراسي")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "تاريخ البداية"), DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [Display(Name = "تاريخ النهاية"), DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    [Display(Name = "العام الحالي")]
    public bool IsCurrent { get; set; }

    [Display(Name = "مغلق")]
    public bool IsClosed { get; set; }

    public ICollection<Term> Terms { get; set; } = new List<Term>();
    public ICollection<Section> Sections { get; set; } = new List<Section>();
}

/// <summary>الفصل الدراسي</summary>
public class Term
{
    public int Id { get; set; }

    [Required, StringLength(50), Display(Name = "الفصل الدراسي")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "الترتيب")]
    public int SeqNo { get; set; }

    [Display(Name = "العام الدراسي")]
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    [Display(Name = "تاريخ البداية"), DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [Display(Name = "تاريخ النهاية"), DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    [Display(Name = "الفصل الحالي")]
    public bool IsCurrent { get; set; }
}

/// <summary>المرحلة الدراسية (ابتدائي / متوسط / ثانوي)</summary>
public class Stage
{
    public int Id { get; set; }

    [Required, StringLength(100), Display(Name = "المرحلة")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "الترتيب")]
    public int SeqNo { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    public ICollection<Grade> Grades { get; set; } = new List<Grade>();
}

/// <summary>الصف الدراسي</summary>
public class Grade
{
    public int Id { get; set; }

    [Required, StringLength(100), Display(Name = "الصف")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "الترتيب")]
    public int SeqNo { get; set; }

    [Display(Name = "المرحلة")]
    public int StageId { get; set; }
    public Stage Stage { get; set; } = null!;

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    public ICollection<Section> Sections { get; set; } = new List<Section>();
}

/// <summary>الشعبة</summary>
public class Section
{
    public int Id { get; set; }

    [Required, StringLength(100), Display(Name = "الشعبة")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "الصف")]
    public int GradeId { get; set; }
    public Grade Grade { get; set; } = null!;

    [Display(Name = "العام الدراسي")]
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    [Display(Name = "السعة")]
    public int Capacity { get; set; } = 30;

    [StringLength(50), Display(Name = "القاعة")]
    public string? Room { get; set; }

    [Display(Name = "رائد الفصل")]
    public int? HomeroomTeacherId { get; set; }
    public Employee? HomeroomTeacher { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    [NotMapped, Display(Name = "الصف/الشعبة")]
    public string FullName => $"{Grade?.Name} - {Name}";

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
}

/// <summary>المادة الدراسية</summary>
public class Subject
{
    public int Id { get; set; }

    [Required, StringLength(30), Display(Name = "رمز المادة")]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(120), Display(Name = "اسم المادة")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "المرحلة")]
    public int? StageId { get; set; }
    public Stage? Stage { get; set; }

    [Display(Name = "الدرجة العظمى"), Column(TypeName = "decimal(6,2)")]
    public decimal MaxScore { get; set; } = 100m;

    [Display(Name = "درجة النجاح"), Column(TypeName = "decimal(6,2)")]
    public decimal PassScore { get; set; } = 50m;

    [Display(Name = "عدد الحصص الأسبوعية")]
    public int WeeklyPeriods { get; set; } = 4;

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;
}

/// <summary>إسناد مادة إلى معلم في شعبة محددة</summary>
public class TeacherSubject
{
    public int Id { get; set; }

    [Display(Name = "المعلم")]
    public int TeacherId { get; set; }
    public Employee Teacher { get; set; } = null!;

    [Display(Name = "المادة")]
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    [Display(Name = "الشعبة")]
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    [Display(Name = "العام الدراسي")]
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;
}

/// <summary>حصة في الجدول الدراسي</summary>
public class TimetableSlot
{
    public int Id { get; set; }

    [Display(Name = "الشعبة")]
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    [Display(Name = "المادة")]
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    [Display(Name = "المعلم")]
    public int TeacherId { get; set; }
    public Employee Teacher { get; set; } = null!;

    [Display(Name = "العام الدراسي")]
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    /// <summary>0 = الأحد ... 6 = السبت</summary>
    [Display(Name = "اليوم")]
    public int DayOfWeek { get; set; }

    [Display(Name = "رقم الحصة")]
    public int PeriodNo { get; set; }

    [Display(Name = "من الساعة")]
    public TimeSpan StartTime { get; set; }

    [Display(Name = "إلى الساعة")]
    public TimeSpan EndTime { get; set; }

    [StringLength(50), Display(Name = "القاعة")]
    public string? Room { get; set; }
}
