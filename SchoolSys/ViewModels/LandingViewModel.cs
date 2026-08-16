namespace SchoolSys.ViewModels;

/// <summary>بيانات الصفحة التعريفية العامة (قبل تسجيل الدخول).</summary>
public class LandingViewModel
{
    public string SchoolName { get; set; } = "";
    public string? SchoolNameEn { get; set; }
    public string? LogoPath { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? YearName { get; set; }

    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    // أرقام حقيقية من قاعدة البيانات
    public int Students { get; set; }
    public int Teachers { get; set; }
    public int Sections { get; set; }
    public int Subjects { get; set; }
    public int Buses { get; set; }
    public int YearsOfService { get; set; }

    public List<StageCard> Stages { get; set; } = [];
}

public class StageCard
{
    public string Name { get; set; } = "";
    public int SeqNo { get; set; }
    public int GradesCount { get; set; }
    public int StudentsCount { get; set; }
    public string GradeRange { get; set; } = "";
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    public string AgeLabel => MinAge.HasValue && MaxAge.HasValue
        ? $"{MinAge} – {MaxAge} سنة"
        : "—";
}
