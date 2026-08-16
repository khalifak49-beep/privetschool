using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolSys.Models;

namespace SchoolSys.ViewModels;

public class TakeAttendanceViewModel
{
    public int? SectionId { get; set; }
    public string? SectionName { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public List<AttendanceEntry> Entries { get; set; } = [];
    public List<SelectListItem> Sections { get; set; } = [];
    public bool AlreadyRecorded { get; set; }
    public bool IsLocked { get; set; }
    public string? LockReason { get; set; }

    public int PresentCount => Entries.Count(e => e.Status == AttendanceStatus.Present);
    public int AbsentCount => Entries.Count(e => e.Status == AttendanceStatus.Absent);
    public int LateCount => Entries.Count(e => e.Status == AttendanceStatus.Late);
    public int ExcusedCount => Entries.Count(e => e.Status == AttendanceStatus.Excused);
}

public class AttendanceEntry
{
    public int StudentId { get; set; }
    public string StudentNo { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string? PhotoPath { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public TimeSpan? CheckInTime { get; set; }
    public int LateMinutes { get; set; }
    public string? Notes { get; set; }
    public int? ExistingId { get; set; }
    public double HistoryRate { get; set; }
}

public class StaffAttendanceViewModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    public EmployeeType? Type { get; set; }
    public List<StaffAttendanceEntry> Entries { get; set; } = [];
    public int PresentCount => Entries.Count(e => e.Status == AttendanceStatus.Present);
    public int AbsentCount => Entries.Count(e => e.Status == AttendanceStatus.Absent);
    public int LateCount => Entries.Count(e => e.Status == AttendanceStatus.Late);
}

public class StaffAttendanceEntry
{
    public int EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = "";
    public string FullName { get; set; } = "";
    public EmployeeType EmployeeType { get; set; }
    public string? PhotoPath { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public TimeSpan? CheckInTime { get; set; }
    public TimeSpan? CheckOutTime { get; set; }
    public int LateMinutes { get; set; }
    public string? Notes { get; set; }
    public int? ExistingId { get; set; }
}

public class AttendanceReportViewModel
{
    public DateTime From { get; set; } = DateTime.Today.AddDays(-30);
    public DateTime To { get; set; } = DateTime.Today;
    public int? StageId { get; set; }
    public int? GradeId { get; set; }
    public int? SectionId { get; set; }
    public string GroupBy { get; set; } = "student";   // student | section | day

    public List<SelectListItem> Stages { get; set; } = [];
    public List<SelectListItem> Grades { get; set; } = [];
    public List<SelectListItem> Sections { get; set; } = [];

    public List<AttendanceReportRow> Rows { get; set; } = [];
    public List<ChartPoint> DailyTrend { get; set; } = [];

    public int TotalRecords { get; set; }
    public int TotalPresent { get; set; }
    public int TotalAbsent { get; set; }
    public int TotalLate { get; set; }
    public int TotalExcused { get; set; }
    public double OverallRate => TotalRecords > 0
        ? Math.Round((double)(TotalPresent + TotalLate) / TotalRecords * 100, 1) : 0;
}

public class AttendanceReportRow
{
    public int Id { get; set; }
    public string Label { get; set; } = "";
    public string? SubLabel { get; set; }
    public int Present { get; set; }
    public int Absent { get; set; }
    public int Late { get; set; }
    public int Excused { get; set; }
    public int Total => Present + Absent + Late + Excused;
    public double Rate => Total > 0 ? Math.Round((double)(Present + Late) / Total * 100, 1) : 0;
}

public class ScanViewModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    public string Target { get; set; } = "student";   // student | staff
    public List<ScanResultRow> Recent { get; set; } = [];
    public int TodayScans { get; set; }
}

public class ScanResultRow
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Section { get; set; }
    public AttendanceStatus Status { get; set; }
    public TimeSpan Time { get; set; }
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
}
