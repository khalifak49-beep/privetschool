using SchoolSys.Models;
using SchoolSys.ViewModels;

namespace SchoolSys.Areas.Portal.ViewModels;

public class StudentPortalViewModel
{
    public Student Student { get; set; } = null!;
    public string? SectionName { get; set; }
    public string? HomeroomTeacher { get; set; }
    public string Currency { get; set; } = "ر.ع";
    public string? TermName { get; set; }

    public AttendanceSummary Attendance { get; set; } = new();
    public decimal AverageScore { get; set; }
    public int Rank { get; set; }
    public int ClassSize { get; set; }
    public int PendingHomework { get; set; }
    public decimal Outstanding { get; set; }

    public List<TodayLessonRow> TodayLessons { get; set; } = [];
    public List<UpcomingExamRow> UpcomingExams { get; set; } = [];
    public List<PortalHomeworkRow> Homework { get; set; } = [];
    public List<AnnouncementRow> Announcements { get; set; } = [];
    public List<StudentSubjectResultRow> RecentResults { get; set; } = [];
}

public class PortalHomeworkRow
{
    public int HomeworkId { get; set; }
    public int SubmissionId { get; set; }
    public string Title { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Teacher { get; set; } = "";
    public string? Description { get; set; }
    public DateTime DueDate { get; set; }
    public decimal MaxScore { get; set; }
    public HomeworkStatus Status { get; set; }
    public decimal? Score { get; set; }
    public string? Feedback { get; set; }
    public string? AttachmentPath { get; set; }
    public string? SubmissionFile { get; set; }
    public string? AnswerText { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public bool IsOverdue => DueDate.Date < DateTime.Today && Status == HomeworkStatus.NotSubmitted;
    public int DaysLeft => (DueDate.Date - DateTime.Today).Days;
}

public class GuardianPortalViewModel
{
    public string GuardianName { get; set; } = "";
    public string Currency { get; set; } = "ر.ع";
    public List<ChildCard> Children { get; set; } = [];
    public List<AnnouncementRow> Announcements { get; set; } = [];
    public decimal TotalOutstanding => Children.Sum(c => c.Outstanding);
    public decimal TotalOverdue => Children.Sum(c => c.Overdue);
}

public class ChildCard
{
    public int StudentId { get; set; }
    public string FullName { get; set; } = "";
    public string StudentNo { get; set; } = "";
    public string? PhotoPath { get; set; }
    public string? Section { get; set; }
    public StudentStatus Status { get; set; }

    public double AttendanceRate { get; set; }
    public int AbsentDays { get; set; }
    public int LateDays { get; set; }
    public bool AbsentToday { get; set; }

    public decimal AveragePercent { get; set; }
    public int Rank { get; set; }
    public int ClassSize { get; set; }

    public int PendingHomework { get; set; }
    public decimal Outstanding { get; set; }
    public decimal Overdue { get; set; }
    public int? InvoiceId { get; set; }

    public string? TransportRoute { get; set; }
    public string? TransportStop { get; set; }
}

public class GuardianChildDetailsViewModel
{
    public Student Student { get; set; } = null!;
    public string? SectionName { get; set; }
    public string? HomeroomTeacher { get; set; }
    public string Currency { get; set; } = "ر.ع";
    public string? TermName { get; set; }

    public AttendanceSummary Attendance { get; set; } = new();
    public List<StudentSubjectResultRow> Results { get; set; } = [];
    public List<PortalHomeworkRow> Homework { get; set; } = [];
    public List<StudentNote> Notes { get; set; } = [];
    public List<TodayLessonRow> Timetable { get; set; } = [];
    public StudentFinanceSummary Finance { get; set; } = new();
    public TransportInfo? Transport { get; set; }
    public List<UpcomingExamRow> UpcomingExams { get; set; } = [];
    public int Rank { get; set; }
    public int ClassSize { get; set; }
    public decimal AveragePercent { get; set; }
}

public class GuardianFeesViewModel
{
    public string Currency { get; set; } = "ر.ع";
    public List<ChildInvoice> Invoices { get; set; } = [];
    public decimal TotalNet => Invoices.Sum(i => i.NetAmount);
    public decimal TotalPaid => Invoices.Sum(i => i.PaidAmount);
    public decimal TotalRemaining => TotalNet - TotalPaid;
}

public class ChildInvoice
{
    public int InvoiceId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = "";
    public string InvoiceNo { get; set; } = "";
    public decimal NetAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Remaining => NetAmount - PaidAmount;
    public InvoiceStatus Status { get; set; }
    public List<Installment> Installments { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
}

public class TeacherPortalViewModel
{
    public string TeacherName { get; set; } = "";
    public List<TeacherSectionRow> Sections { get; set; } = [];
    public List<TodayLessonRow> TodayLessons { get; set; } = [];
    public int PendingHomework { get; set; }
    public int PendingMarks { get; set; }
    public int StudentsCount { get; set; }
}
