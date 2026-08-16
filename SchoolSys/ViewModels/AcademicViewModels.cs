using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolSys.Models;

namespace SchoolSys.ViewModels;

public class AcademicIndexViewModel
{
    public string ActiveTab { get; set; } = "years";
    public List<AcademicYear> Years { get; set; } = [];
    public List<Term> Terms { get; set; } = [];
    public List<StageRow> Stages { get; set; } = [];
    public List<GradeRow> Grades { get; set; } = [];
    public List<SectionRow> Sections { get; set; } = [];
    public List<SubjectRow> Subjects { get; set; } = [];

    public List<SelectListItem> StageOptions { get; set; } = [];
    public List<SelectListItem> GradeOptions { get; set; } = [];
    public List<SelectListItem> YearOptions { get; set; } = [];
    public List<SelectListItem> TeacherOptions { get; set; } = [];

    public int? CurrentYearId { get; set; }
    public string? CurrentYearName { get; set; }
}

public class StageRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int SeqNo { get; set; }
    public bool IsActive { get; set; }
    public int GradesCount { get; set; }
    public int StudentsCount { get; set; }
}

public class GradeRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int SeqNo { get; set; }
    public int StageId { get; set; }
    public string StageName { get; set; } = "";
    public bool IsActive { get; set; }
    public int SectionsCount { get; set; }
    public int StudentsCount { get; set; }
}

public class SectionRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int GradeId { get; set; }
    public string GradeName { get; set; } = "";
    public string StageName { get; set; } = "";
    public int Capacity { get; set; }
    public string? Room { get; set; }
    public int? HomeroomTeacherId { get; set; }
    public string? HomeroomTeacher { get; set; }
    public int StudentsCount { get; set; }
    public bool IsActive { get; set; }
    public double FillRate => Capacity > 0 ? (double)StudentsCount / Capacity * 100 : 0;
}

public class SubjectRow
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int? StageId { get; set; }
    public string? StageName { get; set; }
    public decimal MaxScore { get; set; }
    public decimal PassScore { get; set; }
    public int WeeklyPeriods { get; set; }
    public bool IsActive { get; set; }
    public int TeachersCount { get; set; }
}

// ---------------- توزيع المواد على المعلمين ----------------
public class TeachingLoadViewModel
{
    public int? SectionId { get; set; }
    public int? TeacherId { get; set; }
    public int? SubjectId { get; set; }
    public List<TeachingAssignmentRow> Assignments { get; set; } = [];
    public List<SelectListItem> Sections { get; set; } = [];
    public List<SelectListItem> Teachers { get; set; } = [];
    public List<SelectListItem> Subjects { get; set; } = [];
    public string? CurrentYearName { get; set; }
    public int TotalAssignments { get; set; }
    public int UnassignedSections { get; set; }
}

public class TeachingAssignmentRow
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public string TeacherName { get; set; } = "";
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = "";
    public int SectionId { get; set; }
    public string SectionName { get; set; } = "";
    public int Students { get; set; }
    public bool IsActive { get; set; }
}

// ---------------- الجداول الدراسية ----------------
public class TimetableViewModel
{
    public int? SectionId { get; set; }
    public int? TeacherId { get; set; }
    public string Mode { get; set; } = "section";   // section | teacher
    public string? Title { get; set; }
    public List<SelectListItem> Sections { get; set; } = [];
    public List<SelectListItem> Teachers { get; set; } = [];
    public List<SelectListItem> SubjectOptions { get; set; } = [];
    public List<SelectListItem> TeacherOptions { get; set; } = [];

    /// <summary>الخلايا مفهرسة بـ (اليوم، رقم الحصة).</summary>
    public Dictionary<(int Day, int Period), TimetableCell> Cells { get; set; } = [];

    public int MaxPeriods { get; set; } = 7;
    public int[] Days { get; set; } = [0, 1, 2, 3, 4];
    public List<PeriodTime> PeriodTimes { get; set; } = [];
}

public class TimetableCell
{
    public int Id { get; set; }
    public string Subject { get; set; } = "";
    public string Teacher { get; set; } = "";
    public string Section { get; set; } = "";
    public string? Room { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}

public record PeriodTime(int PeriodNo, TimeSpan Start, TimeSpan End);
