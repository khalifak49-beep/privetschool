using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolSys.Models;
using System.ComponentModel.DataAnnotations;

namespace SchoolSys.ViewModels;

// ===================== الاختبارات =====================
public class ExamIndexViewModel
{
    public int? SectionId { get; set; }
    public int? SubjectId { get; set; }
    public int? TermId { get; set; }
    public ExamStatus? Status { get; set; }
    public string? Q { get; set; }
    public int Page { get; set; } = 1;

    public PagedList<ExamListRow> Exams { get; set; } = PagedList<ExamListRow>.Empty();
    public List<SelectListItem> Sections { get; set; } = [];
    public List<SelectListItem> Subjects { get; set; } = [];
    public List<SelectListItem> Terms { get; set; } = [];

    public int TotalExams { get; set; }
    public int PendingMarks { get; set; }
    public int UpcomingExams { get; set; }
}

public class ExamListRow
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public ExamType ExamType { get; set; }
    public string Subject { get; set; } = "";
    public string Section { get; set; } = "";
    public string Term { get; set; } = "";
    public DateTime ExamDate { get; set; }
    public decimal MaxScore { get; set; }
    public ExamStatus Status { get; set; }
    public int StudentsCount { get; set; }
    public int MarkedCount { get; set; }
    public double MarkedRate => StudentsCount > 0 ? (double)MarkedCount / StudentsCount * 100 : 0;
    public decimal? Average { get; set; }
}

public class ExamFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "الرجاء إدخال عنوان الاختبار")]
    [StringLength(200), Display(Name = "عنوان الاختبار")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "نوع الاختبار")]
    public ExamType ExamType { get; set; } = ExamType.Monthly;

    [Required(ErrorMessage = "الرجاء اختيار المادة")]
    [Display(Name = "المادة")]
    public int? SubjectId { get; set; }

    [Required(ErrorMessage = "الرجاء اختيار الشعبة")]
    [Display(Name = "الشعبة")]
    public int? SectionId { get; set; }

    [Required(ErrorMessage = "الرجاء اختيار الفصل الدراسي")]
    [Display(Name = "الفصل الدراسي")]
    public int? TermId { get; set; }

    [Display(Name = "تاريخ الاختبار"), DataType(DataType.Date)]
    public DateTime ExamDate { get; set; } = DateTime.Today;

    [Display(Name = "وقت البداية")]
    public TimeSpan? StartTime { get; set; }

    [Range(5, 480, ErrorMessage = "المدة يجب أن تكون بين 5 و 480 دقيقة")]
    [Display(Name = "المدة (دقيقة)")]
    public int DurationMinutes { get; set; } = 60;

    [Range(1, 1000, ErrorMessage = "الدرجة العظمى غير صحيحة")]
    [Display(Name = "الدرجة العظمى")]
    public decimal MaxScore { get; set; } = 100;

    [Display(Name = "درجة النجاح")]
    public decimal PassScore { get; set; } = 50;

    [Range(0, 100), Display(Name = "الوزن ضمن مجموع الفصل %")]
    public decimal Weight { get; set; } = 100;

    [Display(Name = "الحالة")]
    public ExamStatus Status { get; set; } = ExamStatus.Draft;

    [StringLength(1000), Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    public List<SelectListItem> Sections { get; set; } = [];
    public List<SelectListItem> Subjects { get; set; } = [];
    public List<SelectListItem> Terms { get; set; } = [];
    public bool IsEdit => Id > 0;
}

public class ExamMarksViewModel
{
    public int ExamId { get; set; }
    public string Title { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Section { get; set; } = "";
    public DateTime ExamDate { get; set; }
    public decimal MaxScore { get; set; }
    public decimal PassScore { get; set; }
    public ExamStatus Status { get; set; }
    public List<MarkEntry> Entries { get; set; } = [];

    public int Marked => Entries.Count(e => e.Score.HasValue || e.IsAbsent);
    public int PassedCount => Entries.Count(e => e.Score.HasValue && e.Score >= PassScore);
    public int FailedCount => Entries.Count(e => e.Score.HasValue && e.Score < PassScore);
    public decimal? Average => Entries.Any(e => e.Score.HasValue)
        ? Math.Round(Entries.Where(e => e.Score.HasValue).Average(e => e.Score!.Value), 2) : null;
    public decimal? Highest => Entries.Where(e => e.Score.HasValue).Max(e => e.Score);
    public decimal? Lowest => Entries.Where(e => e.Score.HasValue).Min(e => e.Score);
}

public class MarkEntry
{
    public int ResultId { get; set; }
    public int StudentId { get; set; }
    public string StudentNo { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string? PhotoPath { get; set; }
    public decimal? Score { get; set; }
    public bool IsAbsent { get; set; }
    public string? Notes { get; set; }
}

// ===================== النتائج =====================
public class ResultsIndexViewModel
{
    public int? SectionId { get; set; }
    public int? TermId { get; set; }
    public string? SectionName { get; set; }
    public string? TermName { get; set; }
    public List<SelectListItem> Sections { get; set; } = [];
    public List<SelectListItem> Terms { get; set; } = [];

    public List<string> Subjects { get; set; } = [];
    public List<StudentResultRow> Rows { get; set; } = [];
    public List<GradeScale> Scales { get; set; } = [];

    public int TotalStudents => Rows.Count;
    public int PassedCount => Rows.Count(r => r.IsPass);
    public double PassRate => Rows.Count > 0 ? Math.Round((double)PassedCount / Rows.Count * 100, 1) : 0;
    public decimal ClassAverage => Rows.Count > 0 ? Math.Round(Rows.Average(r => r.Percentage), 2) : 0;
}

public class StudentResultRow
{
    public int StudentId { get; set; }
    public string StudentNo { get; set; } = "";
    public string StudentName { get; set; } = "";
    public Dictionary<string, decimal?> SubjectScores { get; set; } = [];
    public Dictionary<string, decimal> SubjectMax { get; set; } = [];
    public decimal Total { get; set; }
    public decimal MaxTotal { get; set; }
    public decimal Percentage => MaxTotal > 0 ? Math.Round(Total / MaxTotal * 100m, 2) : 0;
    public int Rank { get; set; }
    public bool IsPass { get; set; }
    public string? GradeLetter { get; set; }
    public string? GradeName { get; set; }
    public string? GradeColor { get; set; }
}

public class ReportCardViewModel
{
    public Student Student { get; set; } = null!;
    public string? SectionName { get; set; }
    public string? StageName { get; set; }
    public string TermName { get; set; } = "";
    public string YearName { get; set; } = "";
    public SchoolSetting Settings { get; set; } = null!;
    public List<ReportCardSubject> Subjects { get; set; } = [];
    public decimal Total { get; set; }
    public decimal MaxTotal { get; set; }
    public decimal Percentage => MaxTotal > 0 ? Math.Round(Total / MaxTotal * 100m, 2) : 0;
    public int Rank { get; set; }
    public int ClassSize { get; set; }
    public string? GradeLetter { get; set; }
    public string? GradeName { get; set; }
    public bool IsPass { get; set; }
    public AttendanceSummary Attendance { get; set; } = new();
    public string? HomeroomTeacher { get; set; }
    public List<SelectListItem> Terms { get; set; } = [];
    public int? TermId { get; set; }
}

public class ReportCardSubject
{
    public string Subject { get; set; } = "";
    public decimal Score { get; set; }
    public decimal MaxScore { get; set; }
    public decimal Percentage => MaxScore > 0 ? Math.Round(Score / MaxScore * 100m, 1) : 0;
    public string? Letter { get; set; }
    public bool IsPass { get; set; }
    public List<(string Title, decimal? Score, decimal Max)> Exams { get; set; } = [];
}

// ===================== الواجبات =====================
public class HomeworkIndexViewModel
{
    public int? SectionId { get; set; }
    public int? SubjectId { get; set; }
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    public PagedList<HomeworkListRow> Items { get; set; } = PagedList<HomeworkListRow>.Empty();
    public List<SelectListItem> Sections { get; set; } = [];
    public List<SelectListItem> Subjects { get; set; } = [];
    public int ActiveCount { get; set; }
    public int PendingGrading { get; set; }
}

public class HomeworkListRow
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Section { get; set; } = "";
    public string Teacher { get; set; } = "";
    public DateTime AssignedDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal MaxScore { get; set; }
    public int Total { get; set; }
    public int Submitted { get; set; }
    public int Graded { get; set; }
    public bool IsPublished { get; set; }
    public bool IsOverdue => DueDate.Date < DateTime.Today;
    public double SubmitRate => Total > 0 ? (double)Submitted / Total * 100 : 0;
}

public class HomeworkFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "الرجاء إدخال عنوان الواجب")]
    [StringLength(200), Display(Name = "عنوان الواجب")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000), Display(Name = "وصف الواجب")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "الرجاء اختيار المادة")]
    [Display(Name = "المادة")]
    public int? SubjectId { get; set; }

    [Required(ErrorMessage = "الرجاء اختيار الشعبة")]
    [Display(Name = "الشعبة")]
    public int? SectionId { get; set; }

    [Display(Name = "المعلم")]
    public int? TeacherId { get; set; }

    [Display(Name = "الفصل الدراسي")]
    public int? TermId { get; set; }

    [Display(Name = "تاريخ التكليف"), DataType(DataType.Date)]
    public DateTime AssignedDate { get; set; } = DateTime.Today;

    [Display(Name = "تاريخ التسليم"), DataType(DataType.Date)]
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(3);

    [Display(Name = "الدرجة العظمى")]
    public decimal MaxScore { get; set; } = 10;

    [Display(Name = "مرفق")]
    public IFormFile? Attachment { get; set; }
    public string? AttachmentPath { get; set; }

    [Display(Name = "منشور للطلاب")]
    public bool IsPublished { get; set; } = true;

    public List<SelectListItem> Sections { get; set; } = [];
    public List<SelectListItem> Subjects { get; set; } = [];
    public List<SelectListItem> Teachers { get; set; } = [];
    public List<SelectListItem> Terms { get; set; } = [];
    public bool IsEdit => Id > 0;
}

public class HomeworkGradeViewModel
{
    public int HomeworkId { get; set; }
    public string Title { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Section { get; set; } = "";
    public DateTime DueDate { get; set; }
    public decimal MaxScore { get; set; }
    public string? Description { get; set; }
    public string? AttachmentPath { get; set; }
    public List<SubmissionEntry> Entries { get; set; } = [];

    public int SubmittedCount => Entries.Count(e => e.Status != HomeworkStatus.NotSubmitted);
    public int GradedCount => Entries.Count(e => e.Score.HasValue);
}

public class SubmissionEntry
{
    public int SubmissionId { get; set; }
    public int StudentId { get; set; }
    public string StudentNo { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string? PhotoPath { get; set; }
    public HomeworkStatus Status { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? FilePath { get; set; }
    public string? AnswerText { get; set; }
    public decimal? Score { get; set; }
    public string? Feedback { get; set; }
}
