using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolSys.Models;

/// <summary>اختبار</summary>
public class Exam
{
    public int Id { get; set; }

    [Required, StringLength(200), Display(Name = "عنوان الاختبار")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "نوع الاختبار")]
    public ExamType ExamType { get; set; } = ExamType.Monthly;

    [Display(Name = "المادة")]
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    [Display(Name = "الشعبة")]
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    [Display(Name = "الفصل الدراسي")]
    public int TermId { get; set; }
    public Term Term { get; set; } = null!;

    [Display(Name = "تاريخ الاختبار"), DataType(DataType.Date)]
    public DateTime ExamDate { get; set; } = DateTime.Today;

    [Display(Name = "وقت البداية")]
    public TimeSpan? StartTime { get; set; }

    [Display(Name = "المدة (دقيقة)")]
    public int DurationMinutes { get; set; } = 60;

    [Display(Name = "الدرجة العظمى"), Column(TypeName = "decimal(6,2)")]
    public decimal MaxScore { get; set; } = 100m;

    [Display(Name = "درجة النجاح"), Column(TypeName = "decimal(6,2)")]
    public decimal PassScore { get; set; } = 50m;

    /// <summary>وزن الاختبار ضمن مجموع الفصل (نسبة مئوية).</summary>
    [Display(Name = "الوزن %"), Column(TypeName = "decimal(5,2)")]
    public decimal Weight { get; set; } = 100m;

    [Display(Name = "الحالة")]
    public ExamStatus Status { get; set; } = ExamStatus.Draft;

    [StringLength(1000), Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<ExamResult> Results { get; set; } = new List<ExamResult>();
}

/// <summary>درجة الطالب في اختبار</summary>
public class ExamResult
{
    public int Id { get; set; }

    [Display(Name = "الاختبار")]
    public int ExamId { get; set; }
    public Exam Exam { get; set; } = null!;

    [Display(Name = "الطالب")]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Display(Name = "الدرجة"), Column(TypeName = "decimal(6,2)")]
    public decimal? Score { get; set; }

    [Display(Name = "غائب")]
    public bool IsAbsent { get; set; }

    [StringLength(400), Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    public int? EnteredByUserId { get; set; }
    public DateTime? EnteredAt { get; set; }

    [NotMapped, Display(Name = "النسبة %")]
    public decimal? Percentage => Score.HasValue && Exam is { MaxScore: > 0 }
        ? Math.Round(Score.Value / Exam.MaxScore * 100m, 2)
        : null;
}

/// <summary>سلّم التقديرات</summary>
public class GradeScale
{
    public int Id { get; set; }

    [Required, StringLength(60), Display(Name = "التقدير")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(5), Display(Name = "الرمز")]
    public string Letter { get; set; } = string.Empty;

    [Display(Name = "من %"), Column(TypeName = "decimal(5,2)")]
    public decimal MinPercent { get; set; }

    [Display(Name = "إلى %"), Column(TypeName = "decimal(5,2)")]
    public decimal MaxPercent { get; set; }

    [Display(Name = "نقاط التقدير"), Column(TypeName = "decimal(4,2)")]
    public decimal Points { get; set; }

    [StringLength(20), Display(Name = "اللون")]
    public string? Color { get; set; }

    [Display(Name = "ناجح")]
    public bool IsPass { get; set; } = true;
}

/// <summary>واجب مدرسي</summary>
public class Homework
{
    public int Id { get; set; }

    [Required, StringLength(200), Display(Name = "عنوان الواجب")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000), Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Display(Name = "المادة")]
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    [Display(Name = "الشعبة")]
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    [Display(Name = "المعلم")]
    public int TeacherId { get; set; }
    public Employee Teacher { get; set; } = null!;

    [Display(Name = "الفصل الدراسي")]
    public int TermId { get; set; }
    public Term Term { get; set; } = null!;

    [Display(Name = "تاريخ التكليف"), DataType(DataType.Date)]
    public DateTime AssignedDate { get; set; } = DateTime.Today;

    [Display(Name = "تاريخ التسليم"), DataType(DataType.Date)]
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(3);

    [Display(Name = "الدرجة العظمى"), Column(TypeName = "decimal(6,2)")]
    public decimal MaxScore { get; set; } = 10m;

    [StringLength(400), Display(Name = "مرفق")]
    public string? AttachmentPath { get; set; }

    [Display(Name = "منشور")]
    public bool IsPublished { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<HomeworkSubmission> Submissions { get; set; } = new List<HomeworkSubmission>();
}

/// <summary>تسليم الطالب للواجب</summary>
public class HomeworkSubmission
{
    public int Id { get; set; }

    [Display(Name = "الواجب")]
    public int HomeworkId { get; set; }
    public Homework Homework { get; set; } = null!;

    [Display(Name = "الطالب")]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Display(Name = "الحالة")]
    public HomeworkStatus Status { get; set; } = HomeworkStatus.NotSubmitted;

    [Display(Name = "تاريخ التسليم")]
    public DateTime? SubmittedAt { get; set; }

    [StringLength(400), Display(Name = "ملف التسليم")]
    public string? FilePath { get; set; }

    [StringLength(2000), Display(Name = "إجابة نصية")]
    public string? AnswerText { get; set; }

    [Display(Name = "الدرجة"), Column(TypeName = "decimal(6,2)")]
    public decimal? Score { get; set; }

    [StringLength(1000), Display(Name = "ملاحظات المعلم")]
    public string? Feedback { get; set; }

    public DateTime? GradedAt { get; set; }
}
