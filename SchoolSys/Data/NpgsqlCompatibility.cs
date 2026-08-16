using System.Runtime.CompilerServices;

namespace SchoolSys.Data;

/// <summary>
/// يضبط سلوك Npgsql مع التواريخ قبل تحميل أي شيء آخر في التجميعة.
///
/// النظام يخزّن التوقيت المحلي (DateTime.Now) تماماً كما يفعل مع SQL Server.
/// بدون هذا الضبط يربط Npgsql النوع DateTime بـ "timestamp with time zone"
/// فيحوّل القيم إلى UTC، ما يُزيح كل التواريخ والأوقات بمقدار فرق المنطقة الزمنية.
///
/// يُنفَّذ عبر ModuleInitializer ليسري في وقت التشغيل و وقت توليد الترحيلات معاً،
/// فتتطابق أعمدة قاعدة البيانات مع سلوك التطبيق.
/// </summary>
internal static class NpgsqlCompatibility
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);
    }
}
