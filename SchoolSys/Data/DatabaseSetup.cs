using Microsoft.EntityFrameworkCore;

namespace SchoolSys.Data;

public enum DbProviderKind { SqlServer, PostgreSql }

/// <summary>
/// يختار مزوّد قاعدة البيانات تلقائياً:
/// PostgreSQL عند النشر (Render يوفّر متغير DATABASE_URL)، و SQL Server محلياً.
/// </summary>
public static class DatabaseSetup
{
    public static DbProviderKind Resolve(IConfiguration config, out string connectionString)
    {
        // 1) Render وبقية منصات الاستضافة تمرّر رابط قاعدة البيانات في DATABASE_URL
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(url))
        {
            connectionString = NormalizePostgres(url);
            return DbProviderKind.PostgreSql;
        }

        // 2) وإلا نأخذ سلسلة الاتصال من الإعدادات
        var cs = config.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException("لم يتم العثور على سلسلة الاتصال 'DefaultConnection'.");

        if (LooksLikePostgres(cs))
        {
            connectionString = NormalizePostgres(cs);
            return DbProviderKind.PostgreSql;
        }

        connectionString = cs;
        return DbProviderKind.SqlServer;
    }

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
