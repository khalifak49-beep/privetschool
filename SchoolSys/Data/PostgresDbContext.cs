using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SchoolSys.Data;

/// <summary>
/// سياق PostgreSQL — يرث نفس النموذج بالكامل من <see cref="ApplicationDbContext"/>
/// لكنه يملك مجموعة ترحيلات (Migrations) خاصة به، لأن ترحيلات EF Core
/// مرتبطة بمزوّد قاعدة البيانات ولا يمكن مشاركتها بين SQL Server و PostgreSQL.
/// يُستخدم تلقائياً عند النشر على Render.
/// </summary>
public class PostgresDbContext : ApplicationDbContext
{
    public PostgresDbContext(DbContextOptions<PostgresDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // PostgreSQL يفضّل الأسماء الصغيرة، لكننا نُبقي الأسماء كما هي
        // لتطابق النموذج بين المزوّدين ويسهل نقل البيانات.
    }
}

/// <summary>
/// مصنع وقت التصميم لسياق PostgreSQL — يُستخدم عند توليد الترحيلات:
/// <c>dotnet ef migrations add X --context PostgresDbContext -o Migrations/Postgres</c>
/// </summary>
public class PostgresDbContextFactory : IDesignTimeDbContextFactory<PostgresDbContext>
{
    public PostgresDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("POSTGRES_DESIGN_CONNECTION")
                         ?? "Host=localhost;Port=5432;Database=schoolsys;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connection)
            .Options;

        return new PostgresDbContext(options);
    }
}

/// <summary>
/// مصنع وقت التصميم لسياق SQL Server.
/// ضروري لأن السياق مسجَّل في الحاوية بتطبيق مشتق (PostgresDbContext) عند النشر،
/// فبدونه قد تُولَّد ترحيلات SQL Server للسياق الخطأ.
/// </summary>
public class SqlServerDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("SQLSERVER_DESIGN_CONNECTION")
                         ?? "Server=.\\SQLEXPRESS;Database=SchoolSysDb;Trusted_Connection=True;" +
                            "TrustServerCertificate=True;MultipleActiveResultSets=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection)
            .Options;

        return new ApplicationDbContext(options);
    }
}
