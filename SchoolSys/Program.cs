using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using QuestPDF.Infrastructure;
using SchoolSys.Data;
using SchoolSys.Hubs;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

// ---------------- بيئة الحاويات (Render / Docker) ----------------
var port = Environment.GetEnvironmentVariable("PORT");
var inContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
                  || !string.IsNullOrEmpty(port);

// منصات الاستضافة تُحدّد المنفذ عبر متغير البيئة PORT
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ---------------- قاعدة البيانات ----------------
// SQL Server محلياً، و PostgreSQL تلقائياً عند وجود DATABASE_URL
var provider = builder.Services.AddApplicationDatabase(builder.Configuration);

// ---------------- الهوية والصلاحيات ----------------
builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 6;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.Name = "SchoolSys.Auth";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = inContainer
        ? CookieSecurePolicy.Always      // Render ينهي TLS عند الوسيط
        : CookieSecurePolicy.SameAsRequest;
});

// حفظ مفاتيح حماية البيانات في قاعدة البيانات حتى لا تُبطَل الجلسات عند إعادة النشر
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("SchoolSys");

// تحديث بيانات الجلسة كل 5 دقائق حتى تسري تغييرات الصلاحيات سريعاً
builder.Services.Configure<SecurityStampValidatorOptions>(o =>
    o.ValidationInterval = TimeSpan.FromMinutes(5));

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// ---------------- الخدمات ----------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IFinanceService, FinanceService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddSingleton<IQrService, QrService>();
builder.Services.AddSingleton<IExportService, ExportService>();

builder.Services.AddSignalR();
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

// زرع البيانات التجريبية في الخلفية بعد الإقلاع
builder.Services.AddHostedService<DemoDataHostedService>();

// الثقة برؤوس الوسيط العكسي (Render / أي reverse proxy)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// ---------------- الثقافة العربية ----------------
var culture = new CultureInfo("ar-OM");
culture.NumberFormat.DigitSubstitution = DigitShapes.None;

// استخدام الفاصلة والنقطة الغربية بدل العلامات العربية لسهولة القراءة في الجداول والتقارير
culture.NumberFormat.NumberGroupSeparator = ",";
culture.NumberFormat.NumberDecimalSeparator = ".";
culture.NumberFormat.CurrencyGroupSeparator = ",";
culture.NumberFormat.CurrencyDecimalSeparator = ".";
culture.NumberFormat.PercentGroupSeparator = ",";
culture.NumberFormat.PercentDecimalSeparator = ".";

CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(culture),
    SupportedCultures = [culture],
    SupportedUICultures = [culture]
});

// يجب أن يسبق بقية الوسائط ليعرف التطبيق أن الطلب الأصلي كان HTTPS
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    if (!inContainer) app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");

// داخل الحاوية ينهي الوسيط TLS، فإعادة التوجيه هنا تسبب حلقة لا نهائية
if (!inContainer) app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// الصفحة التعريفية العامة هي الواجهة الافتراضية، ولوحة التحكم خلف تسجيل الدخول
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHealthChecks("/healthz");

// ---------------- تهيئة قاعدة البيانات ----------------
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("مزوّد قاعدة البيانات: {Provider}", provider);
        logger.LogInformation("جارٍ تطبيق الترحيلات والتهيئة الأساسية...");

        await DbSeeder.MigrateAndSeedCoreAsync(app.Services);

        logger.LogInformation("اكتملت التهيئة الأساسية. البيانات التجريبية ستُزرع في الخلفية.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "فشل في تهيئة قاعدة البيانات.");
        throw;
    }
}

app.Run();
