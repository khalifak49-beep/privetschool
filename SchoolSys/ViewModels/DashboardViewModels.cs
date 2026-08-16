using SchoolSys.Models;

namespace SchoolSys.ViewModels;

public class DashboardViewModel
{
    public string SchoolName { get; set; } = "";
    public string Currency { get; set; } = "ر.ع";
    public string? AcademicYearName { get; set; }
    public string? TermName { get; set; }

    // مؤشرات رئيسية
    public int TotalStudents { get; set; }
    public int TotalTeachers { get; set; }
    public int TotalSections { get; set; }
    public int TotalGuardians { get; set; }
    public int NewStudentsThisMonth { get; set; }

    // الحضور
    public int PresentToday { get; set; }
    public int AbsentToday { get; set; }
    public int LateToday { get; set; }
    public int RecordedToday { get; set; }
    public double AttendanceRateToday { get; set; }
    public bool AttendanceTakenToday => RecordedToday > 0;

    // المالية
    public decimal CollectedThisMonth { get; set; }
    public decimal ExpectedTotal { get; set; }
    public decimal CollectedTotal { get; set; }
    public decimal OverdueAmount { get; set; }
    public int OverdueStudentsCount { get; set; }
    public double CollectionRate => ExpectedTotal > 0 ? (double)(CollectedTotal / ExpectedTotal * 100m) : 0;

    // الرسوم البيانية
    public List<ChartPoint> AttendanceTrend { get; set; } = [];
    public List<ChartPoint> StudentsByStage { get; set; } = [];
    public List<ChartPoint> RevenueByMonth { get; set; } = [];
    public List<ChartPoint> GenderSplit { get; set; } = [];

    // قوائم
    public List<UpcomingExamRow> UpcomingExams { get; set; } = [];
    public List<AnnouncementRow> Announcements { get; set; } = [];
    public List<RecentPaymentRow> RecentPayments { get; set; } = [];
    public List<AbsentStudentRow> AbsentStudents { get; set; } = [];
    public List<SectionLoadRow> TopSections { get; set; } = [];
}

public record ChartPoint(string Label, decimal Value);

public class UpcomingExamRow
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Section { get; set; } = "";
    public DateTime ExamDate { get; set; }
    public ExamType ExamType { get; set; }
    public int DaysLeft => (ExamDate.Date - DateTime.Today).Days;
}

public class AnnouncementRow
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime PublishDate { get; set; }
    public AnnouncementAudience Audience { get; set; }
    public bool IsPinned { get; set; }
}

public class RecentPaymentRow
{
    public int Id { get; set; }
    public string ReceiptNo { get; set; } = "";
    public string StudentName { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public PaymentMethod Method { get; set; }
}

public class AbsentStudentRow
{
    public int StudentId { get; set; }
    public string StudentNo { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string Section { get; set; } = "";
    public AttendanceStatus Status { get; set; }
    public bool GuardianNotified { get; set; }
}

public class SectionLoadRow
{
    public int SectionId { get; set; }
    public string Name { get; set; } = "";
    public int Students { get; set; }
    public int Capacity { get; set; }
    public double FillRate => Capacity > 0 ? (double)Students / Capacity * 100 : 0;
}

/// <summary>لوحة تحكم المعلم.</summary>
public class TeacherDashboardViewModel
{
    public string TeacherName { get; set; } = "";
    public int SectionsCount { get; set; }
    public int StudentsCount { get; set; }
    public int SubjectsCount { get; set; }
    public int PendingHomework { get; set; }
    public List<TodayLessonRow> TodayLessons { get; set; } = [];
    public List<TeacherSectionRow> Sections { get; set; } = [];
    public List<UpcomingExamRow> UpcomingExams { get; set; } = [];
    public List<AnnouncementRow> Announcements { get; set; } = [];
}

public class TodayLessonRow
{
    public int PeriodNo { get; set; }
    public string Subject { get; set; } = "";
    public string Section { get; set; } = "";
    public string? Room { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int SectionId { get; set; }
}

public class TeacherSectionRow
{
    public int SectionId { get; set; }
    public string Name { get; set; } = "";
    public string Subject { get; set; } = "";
    public int Students { get; set; }
}
