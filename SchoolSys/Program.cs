using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using SchoolSys.Data;
using SchoolSys.Hubs;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

// ---------------- قاعدة البيانات ----------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("لم يتم العثور على سلسلة الاتصال 'DefaultConnection'.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(3);
        sql.CommandTimeout(180);
    }));

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
});

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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");
app.UseHttpsRedirection();
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

// ---------------- تهيئة قاعدة البيانات ----------------
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("جارٍ تهيئة قاعدة البيانات وزرع البيانات التجريبية...");
        await DbSeeder.SeedAsync(app.Services);
        logger.LogInformation("اكتملت تهيئة قاعدة البيانات.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "فشل في تهيئة قاعدة البيانات.");
        throw;
    }
}

app.Run();
