using Microsoft.EntityFrameworkCore;

namespace SchoolSys.Data;

public enum DbProviderKind { SqlServer, PostgreSql }

/// <summary>
/// يختار مزوّد قاعدة البيانات تلقائياً:
/// PostgreSQL عند النشر (Render يوفّر متغير DATABASE_URL)، و SQL Server محلياً.
/// </summary>
public static class DatabaseSetup
{
    /// <summary>هل نعمل داخل حاوية على منصة استضافة؟</summary>
    public static bool IsHosted =>
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PORT"));

    public static DbProviderKind Resolve(IConfiguration config, out string connectionString)
    {
        // 1) Render وبقية منصات الاستضافة تمرّر رابط قاعدة البيانات في DATABASE_URL
        // (بعض المنصات تستخدم أسماء أخرى، لذا نجرّبها جميعاً)
        foreach (var name in new[] { "DATABASE_URL", "POSTGRES_URL", "POSTGRESQL_URL" })
        {
            var url = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(url))
            {
                connectionString = NormalizePostgres(url);
                return DbProviderKind.PostgreSql;
            }
        }

        // 2) وإلا نأخذ سلسلة الاتصال من الإعدادات
        var cs = config.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException("لم يتم العثور على سلسلة الاتصال 'DefaultConnection'.");

        if (LooksLikePostgres(cs))
        {
            connectionString = NormalizePostgres(cs);
            return DbProviderKind.PostgreSql;
        }

        // 3) حارس النشر: سلسلة SQL Server المحلية لا تعمل داخل حاوية لينكس.
        // نفشل هنا برسالة واضحة بدل خطأ شبكة غامض بعد دقيقة من المحاولات.
        if (IsHosted && IsLocalSqlServer(cs))
        {
            throw new InvalidOperationException(
                "\n" +
                "════════════════════════════════════════════════════════════════\n" +
                "  خطأ في الإعداد: لم يتم ضبط قاعدة بيانات للنشر\n" +
                "════════════════════════════════════════════════════════════════\n" +
                "  التطبيق يعمل داخل حاوية، لكن متغير البيئة DATABASE_URL غير موجود،\n" +
                "  فرجع إلى سلسلة اتصال SQL Server المحلية وهي غير متاحة هنا.\n" +
                "\n" +
                "  الحل على Render:\n" +
                "   1. أنشئ قاعدة بيانات: New ➜ PostgreSQL (الخطة المجانية)\n" +
                "   2. انسخ قيمة Internal Database URL منها\n" +
                "   3. في خدمة الويب: Environment ➜ Add Environment Variable\n" +
                "        Key   = DATABASE_URL\n" +
                "        Value = الرابط المنسوخ\n" +
                "   4. احفظ — ستُعاد عملية النشر تلقائياً\n" +
                "\n" +
                "  أو استخدم Blueprint فيتم كل ذلك تلقائياً من ملف render.yaml:\n" +
                "        New ➜ Blueprint ➜ اختر المستودع ➜ Apply\n" +
                "════════════════════════════════════════════════════════════════\n");
        }

        connectionString = cs;
        return DbProviderKind.SqlServer;
    }

    private static bool IsLocalSqlServer(string cs) =>
        cs.Contains("SQLEXPRESS", StringComparison.OrdinalIgnoreCase) ||
        cs.Contains("Server=.", StringComparison.OrdinalIgnoreCase) ||
        cs.Contains("Server=(local", StringComparison.OrdinalIgnoreCase) ||
        cs.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
        cs.Contains("Trusted_Connection", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikePostgres(string cs) =>
        cs.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        cs.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
        cs.Contains("Host=", StringComparison.OrdinalIgnoreCase) &&
        cs.Contains("Username=", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// يحوّل رابطاً بصيغة postgres://user:pass@host:port/db إلى صيغة Npgsql،
    /// ويمرّر سلاسل Npgsql الجاهزة كما هي بعد ضمان تفعيل SSL.
    /// </summary>
    public static string NormalizePostgres(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return EnsureSsl(value);
        }

        var uri = new Uri(value);
        var parts = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(parts[0]);
        var password = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
        var database = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 5432;

        var built =
            $"Host={uri.Host};Port={port};Database={database};" +
            $"Username={user};Password={password};" +
            "SSL Mode=Require;Trust Server Certificate=true;" +
            "Pooling=true;Minimum Pool Size=0;Maximum Pool Size=20;" +
            "Timeout=30;Command Timeout=180";

        return built;
    }

    private static string EnsureSsl(string cs)
    {
        if (cs.Contains("SSL Mode", StringComparison.OrdinalIgnoreCase) ||
            cs.Contains("SslMode", StringComparison.OrdinalIgnoreCase))
            return cs;

        // الاتصال المحلي بلا SSL، وأي مضيف خارجي يحتاجه
        var isLocal = cs.Contains("Host=localhost", StringComparison.OrdinalIgnoreCase) ||
                      cs.Contains("Host=127.0.0.1", StringComparison.OrdinalIgnoreCase);

        return isLocal ? cs : cs.TrimEnd(';') + ";SSL Mode=Require;Trust Server Certificate=true";
    }

    /// <summary>يسجّل السياق المناسب في حاوية الخدمات.</summary>
    public static DbProviderKind AddApplicationDatabase(this IServiceCollection services, IConfiguration config)
    {
        var kind = Resolve(config, out var cs);

        if (kind == DbProviderKind.PostgreSql)
        {
            // يُبقي سلوك DateTime مطابقاً لـ SQL Server (بلا تحويل للمنطقة الزمنية)
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);

            services.AddDbContext<ApplicationDbContext, PostgresDbContext>(options =>
                options.UseNpgsql(cs, npg =>
                {
                    npg.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                    npg.CommandTimeout(180);
                }));
        }
        else
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(cs, sql =>
                {
                    sql.EnableRetryOnFailure(3);
                    sql.CommandTimeout(180);
                }));
        }

        return kind;
    }
}
