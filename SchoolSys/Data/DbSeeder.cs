using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Models;
using SchoolSys.Security;
using System.Security.Claims;

namespace SchoolSys.Data;

/// <summary>تهيئة قاعدة البيانات: الأدوار، الصلاحيات، المستخدم الأساسي، والبنية الأكاديمية.</summary>
public static class DbSeeder
{
    public const string DefaultAdminEmail = "admin@school.local";
    public const string DefaultPassword = "Admin@123";

    /// <summary>
    /// التهيئة الأساسية السريعة: الترحيلات، الأدوار، الإعدادات، سلّم التقديرات، حساب المسؤول.
    /// تُنفَّذ قبل بدء استقبال الطلبات.
    /// </summary>
    public static async Task MigrateAndSeedCoreAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<ApplicationRole>>();

        await db.Database.MigrateAsync();

        await SeedRolesAsync(roleManager);
        await SeedSettingsAsync(db);
        await SeedGradeScalesAsync(db);
        await SeedAdminAsync(userManager);
    }

    /// <summary>
    /// البيانات التجريبية الضخمة (1250 طالب وما يتبعها).
    /// تُنفَّذ في الخلفية بعد إقلاع التطبيق حتى لا تؤخّر فحص الجاهزية على منصات الاستضافة.
    /// </summary>
    public static async Task SeedDemoAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        await DemoDataSeeder.SeedAsync(db, userManager);
    }

    // ------------------------------------------------------------------
    // الأدوار والصلاحيات
    // ------------------------------------------------------------------
    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        foreach (var (roleName, permissions) in RolePermissionMap)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                role = new ApplicationRole
                {
                    Name = roleName,
                    DisplayName = RoleNames.Display(roleName),
                    IsSystemRole = true,
                    Description = $"دور {RoleNames.Display(roleName)}"
                };
                var created = await roleManager.CreateAsync(role);
                if (!created.Succeeded) continue;
            }

            var existing = (await roleManager.GetClaimsAsync(role))
                .Where(c => c.Type == Permissions.ClaimType)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var p in permissions.Where(p => !existing.Contains(p)))
                await roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, p));
        }
    }

    /// <summary>خريطة الصلاحيات الافتراضية لكل دور.</summary>
    public static readonly Dictionary<string, string[]> RolePermissionMap = new()
    {
        [RoleNames.SuperAdmin] = Permissions.AllPermissions.ToArray(),

        [RoleNames.Principal] =
        [
            Permissions.DashboardView, Permissions.DashboardFinance,
            Permissions.StudentsView, Permissions.StudentsCreate, Permissions.StudentsEdit,
            Permissions.StudentsTransfer, Permissions.StudentsDocuments, Permissions.StudentsNotes,
            Permissions.GuardiansView, Permissions.GuardiansCreate, Permissions.GuardiansEdit,
            Permissions.EmployeesView, Permissions.EmployeesCreate, Permissions.EmployeesEdit,
            Permissions.AcademicView, Permissions.AcademicManage, Permissions.AcademicAssignSubjects,
            Permissions.TimetableView, Permissions.TimetableManage,
            Permissions.AttendanceView, Permissions.AttendanceTakeStudents, Permissions.AttendanceTakeStaff,
            Permissions.AttendanceReports,
            Permissions.ExamsView, Permissions.ExamsManage, Permissions.ExamsApprove,
            Permissions.ResultsView, Permissions.ResultsCertificates,
            Permissions.HomeworkView,
            Permissions.FinanceView, Permissions.FinanceInvoices, Permissions.FinanceDiscounts,
            Permissions.FinanceReports,
            Permissions.TransportView, Permissions.TransportManage,
            Permissions.MessagesUse, Permissions.AnnouncementsView, Permissions.AnnouncementsManage,
            Permissions.NotificationsSend,
            Permissions.ReportsView, Permissions.ReportsExport,
            Permissions.UsersView, Permissions.RolesView, Permissions.AuditView
        ],

        [RoleNames.VicePrincipal] =
        [
            Permissions.DashboardView,
            Permissions.StudentsView, Permissions.StudentsCreate, Permissions.StudentsEdit,
            Permissions.StudentsTransfer, Permissions.StudentsDocuments, Permissions.StudentsNotes,
            Permissions.GuardiansView, Permissions.GuardiansEdit,
            Permissions.EmployeesView,
            Permissions.AcademicView, Permissions.AcademicManage, Permissions.AcademicAssignSubjects,
            Permissions.TimetableView, Permissions.TimetableManage,
            Permissions.AttendanceView, Permissions.AttendanceTakeStudents, Permissions.AttendanceTakeStaff,
            Permissions.AttendanceReports,
            Permissions.ExamsView, Permissions.ExamsManage, Permissions.ExamsApprove,
            Permissions.ResultsView, Permissions.ResultsCertificates,
            Permissions.HomeworkView,
            Permissions.TransportView,
            Permissions.MessagesUse, Permissions.AnnouncementsView, Permissions.AnnouncementsManage,
            Permissions.NotificationsSend,
            Permissions.ReportsView, Permissions.ReportsExport
        ],

        [RoleNames.AcademicAdmin] =
        [
            Permissions.DashboardView,
            Permissions.StudentsView, Permissions.StudentsEdit, Permissions.StudentsTransfer,
            Permissions.StudentsNotes,
            Permissions.GuardiansView,
            Permissions.EmployeesView,
            Permissions.AcademicView, Permissions.AcademicManage, Permissions.AcademicAssignSubjects,
            Permissions.TimetableView, Permissions.TimetableManage,
            Permissions.AttendanceView, Permissions.AttendanceReports,
            Permissions.ExamsView, Permissions.ExamsManage, Permissions.ExamsEnterMarks, Permissions.ExamsApprove,
            Permissions.ResultsView, Permissions.ResultsCertificates,
            Permissions.HomeworkView,
            Permissions.MessagesUse, Permissions.AnnouncementsView, Permissions.AnnouncementsManage,
            Permissions.ReportsView, Permissions.ReportsExport
        ],

        [RoleNames.Accountant] =
        [
            Permissions.DashboardView, Permissions.DashboardFinance,
            Permissions.StudentsView, Permissions.GuardiansView,
            Permissions.FinanceView, Permissions.FinanceFeeItems, Permissions.FinanceInvoices,
            Permissions.FinancePayments, Permissions.FinanceDiscounts, Permissions.FinanceCancelPayment,
            Permissions.FinanceReports,
            Permissions.TransportView,
            Permissions.MessagesUse, Permissions.AnnouncementsView, Permissions.NotificationsSend,
            Permissions.ReportsView, Permissions.ReportsExport
        ],

        [RoleNames.Teacher] =
        [
            Permissions.DashboardView,
            Permissions.StudentsView, Permissions.StudentsNotes,
            Permissions.AcademicView, Permissions.TimetableView,
            Permissions.AttendanceView, Permissions.AttendanceTakeStudents,
            Permissions.ExamsView, Permissions.ExamsManage, Permissions.ExamsEnterMarks,
            Permissions.ResultsView,
            Permissions.HomeworkView, Permissions.HomeworkManage, Permissions.HomeworkGrade,
            Permissions.MessagesUse, Permissions.AnnouncementsView,
            Permissions.ReportsView
        ],

        [RoleNames.TransportManager] =
        [
            Permissions.DashboardView,
            Permissions.StudentsView, Permissions.GuardiansView, Permissions.EmployeesView,
            Permissions.TransportView, Permissions.TransportManage, Permissions.TransportLog,
            Permissions.MessagesUse, Permissions.AnnouncementsView, Permissions.NotificationsSend,
            Permissions.ReportsView, Permissions.ReportsExport
        ],

        [RoleNames.Receptionist] =
        [
            Permissions.DashboardView,
            Permissions.StudentsView, Permissions.StudentsCreate, Permissions.StudentsDocuments,
            Permissions.GuardiansView, Permissions.GuardiansCreate, Permissions.GuardiansEdit,
            Permissions.EmployeesView,
            Permissions.AttendanceView, Permissions.AttendanceTakeStudents,
            Permissions.TimetableView,
            Permissions.MessagesUse, Permissions.AnnouncementsView,
            Permissions.ReportsView
        ],

        [RoleNames.Student] =
        [
            Permissions.DashboardView,
            Permissions.TimetableView, Permissions.AttendanceView,
            Permissions.ResultsView, Permissions.HomeworkView, Permissions.HomeworkSubmit,
            Permissions.MessagesUse, Permissions.AnnouncementsView
        ],

        [RoleNames.Guardian] =
        [
            Permissions.DashboardView,
            Permissions.TimetableView, Permissions.AttendanceView,
            Permissions.ResultsView, Permissions.HomeworkView,
            Permissions.FinanceView, Permissions.TransportView,
            Permissions.MessagesUse, Permissions.AnnouncementsView
        ]
    };

    // ------------------------------------------------------------------
    private static async Task SeedSettingsAsync(ApplicationDbContext db)
    {
        if (await db.SchoolSettings.AnyAsync()) return;

        db.SchoolSettings.Add(new SchoolSetting
        {
            SchoolName = "مدرسة النخبة الأهلية",
            SchoolNameEn = "Al-Nokhba Private School",
            Address = "مسقط - سلطنة عُمان",
            Phone = "+968 2400 0000",
            Email = "info@alnokhba.edu.om",
            Website = "www.alnokhba.edu.om",
            Currency = "ر.ع",
            SchoolStartTime = new TimeSpan(7, 15, 0),
            SchoolEndTime = new TimeSpan(13, 30, 0),
            LateGraceMinutes = 10,
            AutoNotifyGuardianOnAbsence = true
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedGradeScalesAsync(ApplicationDbContext db)
    {
        if (await db.GradeScales.AnyAsync()) return;

        db.GradeScales.AddRange(
            new GradeScale { Name = "ممتاز مرتفع", Letter = "A+", MinPercent = 95, MaxPercent = 100, Points = 4.00m, Color = "#16a34a", IsPass = true },
            new GradeScale { Name = "ممتاز", Letter = "A", MinPercent = 90, MaxPercent = 94.99m, Points = 3.75m, Color = "#22c55e", IsPass = true },
            new GradeScale { Name = "جيد جداً مرتفع", Letter = "B+", MinPercent = 85, MaxPercent = 89.99m, Points = 3.50m, Color = "#84cc16", IsPass = true },
            new GradeScale { Name = "جيد جداً", Letter = "B", MinPercent = 80, MaxPercent = 84.99m, Points = 3.00m, Color = "#a3e635", IsPass = true },
            new GradeScale { Name = "جيد مرتفع", Letter = "C+", MinPercent = 75, MaxPercent = 79.99m, Points = 2.50m, Color = "#facc15", IsPass = true },
            new GradeScale { Name = "جيد", Letter = "C", MinPercent = 70, MaxPercent = 74.99m, Points = 2.00m, Color = "#fbbf24", IsPass = true },
            new GradeScale { Name = "مقبول مرتفع", Letter = "D+", MinPercent = 65, MaxPercent = 69.99m, Points = 1.50m, Color = "#fb923c", IsPass = true },
            new GradeScale { Name = "مقبول", Letter = "D", MinPercent = 50, MaxPercent = 64.99m, Points = 1.00m, Color = "#f97316", IsPass = true },
            new GradeScale { Name = "راسب", Letter = "F", MinPercent = 0, MaxPercent = 49.99m, Points = 0.00m, Color = "#dc2626", IsPass = false }
        );
        await db.SaveChangesAsync();
    }

    private static async Task SeedAdminAsync(UserManager<ApplicationUser> userManager)
    {
        if (await userManager.FindByEmailAsync(DefaultAdminEmail) is not null) return;

        var admin = new ApplicationUser
        {
            UserName = DefaultAdminEmail,
            Email = DefaultAdminEmail,
            EmailConfirmed = true,
            FullName = "مسؤول النظام",
            IsActive = true
        };

        var result = await userManager.CreateAsync(admin, DefaultPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, RoleNames.SuperAdmin);
    }
}
