using Microsoft.AspNetCore.Html;
using SchoolSys.Models;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace SchoolSys.Helpers;

public static class ViewHelpers
{
    /// <summary>يقرأ الاسم العربي من سمة [Display] على عنصر التعداد.</summary>
    public static string Display(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>()?.Name ?? value.ToString();
    }

    public static IEnumerable<(T Value, string Text)> Options<T>() where T : struct, Enum
        => Enum.GetValues<T>().Select(v => (v, ((Enum)(object)v).Display()));

    public static string Money(decimal value, string currency = "ر.ع")
        => $"{value:N2} {currency}";

    public static string Date(DateTime? value) => value?.ToString("yyyy/MM/dd") ?? "—";
    public static string DateTimeShort(DateTime? value) => value?.ToString("yyyy/MM/dd HH:mm") ?? "—";
    public static string Time(TimeSpan? value) => value.HasValue ? DateTime.Today.Add(value.Value).ToString("hh:mm tt") : "—";

    public static string DayName(int dayOfWeek) => dayOfWeek switch
    {
        0 => "الأحد",
        1 => "الاثنين",
        2 => "الثلاثاء",
        3 => "الأربعاء",
        4 => "الخميس",
        5 => "الجمعة",
        6 => "السبت",
        _ => "-"
    };

    /// <summary>وصف زمني مختصر: منذ 5 دقائق ...</summary>
    public static string Ago(DateTime value)
    {
        var diff = DateTime.Now - value;
        if (diff.TotalMinutes < 1) return "الآن";
        if (diff.TotalMinutes < 60) return $"منذ {(int)diff.TotalMinutes} دقيقة";
        if (diff.TotalHours < 24) return $"منذ {(int)diff.TotalHours} ساعة";
        if (diff.TotalDays < 30) return $"منذ {(int)diff.TotalDays} يوم";
        return value.ToString("yyyy/MM/dd");
    }

    // ---------------- شارات الحالة ----------------
    private static HtmlString Chip(string text, string tone, string? icon = null)
        => new($"<span class=\"chip tone-{tone}\">{(icon is null ? "" : $"<i class=\"bi {icon}\"></i>")}{text}</span>");

    public static HtmlString Badge(this AttendanceStatus s) => s switch
    {
        AttendanceStatus.Present => Chip(s.Display(), "ok", "bi-check-circle-fill"),
        AttendanceStatus.Absent => Chip(s.Display(), "danger", "bi-x-circle-fill"),
        AttendanceStatus.Late => Chip(s.Display(), "warn", "bi-clock-fill"),
        AttendanceStatus.Excused => Chip(s.Display(), "info", "bi-file-earmark-check"),
        _ => Chip(s.Display(), "purple", "bi-box-arrow-left")
    };

    public static HtmlString Badge(this StudentStatus s) => s switch
    {
        StudentStatus.Active => Chip(s.Display(), "ok"),
        StudentStatus.Graduated => Chip(s.Display(), "info"),
        StudentStatus.Transferred => Chip(s.Display(), "warn"),
        StudentStatus.Suspended => Chip(s.Display(), "danger"),
        _ => Chip(s.Display(), "brand")
    };

    public static HtmlString Badge(this InvoiceStatus s) => s switch
    {
        InvoiceStatus.Paid => Chip(s.Display(), "ok"),
        InvoiceStatus.PartiallyPaid => Chip(s.Display(), "warn"),
        InvoiceStatus.Unpaid => Chip(s.Display(), "danger"),
        _ => Chip(s.Display(), "brand")
    };

    public static HtmlString Badge(this InstallmentStatus s) => s switch
    {
        InstallmentStatus.Paid => Chip(s.Display(), "ok"),
        InstallmentStatus.Partial => Chip(s.Display(), "warn"),
        InstallmentStatus.Overdue => Chip(s.Display(), "danger"),
        _ => Chip(s.Display(), "info")
    };

    public static HtmlString Badge(this ExamStatus s) => s switch
    {
        ExamStatus.Draft => Chip(s.Display(), "brand"),
        ExamStatus.Published => Chip(s.Display(), "info"),
        ExamStatus.Graded => Chip(s.Display(), "warn"),
        _ => Chip(s.Display(), "ok")
    };

    public static HtmlString Badge(this HomeworkStatus s) => s switch
    {
        HomeworkStatus.Graded => Chip(s.Display(), "ok"),
        HomeworkStatus.Submitted => Chip(s.Display(), "info"),
        HomeworkStatus.Late => Chip(s.Display(), "warn"),
        _ => Chip(s.Display(), "danger")
    };

    public static HtmlString Badge(this NoteSeverity s) => s switch
    {
        NoteSeverity.Positive => Chip(s.Display(), "ok"),
        NoteSeverity.Info => Chip(s.Display(), "info"),
        NoteSeverity.Warning => Chip(s.Display(), "warn"),
        _ => Chip(s.Display(), "danger")
    };

    public static HtmlString Badge(this OutboxStatus s) => s switch
    {
        OutboxStatus.Sent => Chip(s.Display(), "ok"),
        OutboxStatus.Queued => Chip(s.Display(), "info"),
        OutboxStatus.Failed => Chip(s.Display(), "danger"),
        _ => Chip(s.Display(), "brand")
    };

    public static HtmlString Badge(this EmployeeType t) => Chip(t.Display(),
        t switch
        {
            EmployeeType.Teacher => "brand",
            EmployeeType.Accountant => "ok",
            EmployeeType.Driver or EmployeeType.BusSupervisor => "warn",
            EmployeeType.Admin => "purple",
            _ => "info"
        });

    /// <summary>لون تقدير حسب النسبة المئوية.</summary>
    public static string ScoreTone(decimal percent) => percent switch
    {
        >= 85 => "ok",
        >= 70 => "info",
        >= 50 => "warn",
        _ => "danger"
    };

    public static HtmlString ScoreChip(decimal? score, decimal max)
    {
        if (score is null) return Chip("غائب", "danger");
        var pct = max > 0 ? score.Value / max * 100m : 0;
        return new HtmlString(
            $"<span class=\"chip tone-{ScoreTone(pct)} num\">{score.Value:0.##} / {max:0.##}</span>");
    }

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "؟";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[1][0]}" : name[..Math.Min(2, name.Length)];
    }
}
