using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Models;

namespace SchoolSys.Data;

public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, int,
        IdentityUserClaim<int>, ApplicationUserRole, IdentityUserLogin<int>,
        IdentityRoleClaim<int>, IdentityUserToken<int>>,
      IDataProtectionKeyContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    /// <summary>مُنشئ محمي تستخدمه السياقات المشتقة (PostgreSQL).</summary>
    protected ApplicationDbContext(DbContextOptions options) : base(options) { }

    /// <summary>
    /// تخزين مفاتيح حماية البيانات في قاعدة البيانات بدل نظام الملفات،
    /// حتى لا تُبطَل جلسات المستخدمين عند إعادة تشغيل الحاوية أو النشر.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // الأكاديمي
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Term> Terms => Set<Term>();
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();
    public DbSet<TimetableSlot> TimetableSlots => Set<TimetableSlot>();

    // الأشخاص
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<StudentTransfer> StudentTransfers => Set<StudentTransfer>();
    public DbSet<StudentDocument> StudentDocuments => Set<StudentDocument>();
    public DbSet<StudentNote> StudentNotes => Set<StudentNote>();

    // الحضور
    public DbSet<StudentAttendance> StudentAttendances => Set<StudentAttendance>();
    public DbSet<StaffAttendance> StaffAttendances => Set<StaffAttendance>();

    // التقييم
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();
    public DbSet<GradeScale> GradeScales => Set<GradeScale>();
    public DbSet<Homework> Homeworks => Set<Homework>();
    public DbSet<HomeworkSubmission> HomeworkSubmissions => Set<HomeworkSubmission>();

    // المالية
    public DbSet<FeeItem> FeeItems => Set<FeeItem>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Discount> Discounts => Set<Discount>();

    // النقل
    public DbSet<Bus> Buses => Set<Bus>();
    public DbSet<TransportRoute> TransportRoutes => Set<TransportRoute>();
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();
    public DbSet<StudentTransport> StudentTransports => Set<StudentTransport>();
    public DbSet<TransportLog> TransportLogs => Set<TransportLog>();

    // التواصل
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageRecipient> MessageRecipients => Set<MessageRecipient>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    // النظام
    public DbSet<SchoolSetting> SchoolSettings => Set<SchoolSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ----- جداول Identity بأسماء مختصرة -----
        b.Entity<ApplicationUser>().ToTable("Users");
        b.Entity<ApplicationRole>().ToTable("Roles");
        b.Entity<ApplicationUserRole>().ToTable("UserRoles");
        b.Entity<IdentityUserClaim<int>>().ToTable("UserClaims");
        b.Entity<IdentityUserLogin<int>>().ToTable("UserLogins");
        b.Entity<IdentityRoleClaim<int>>().ToTable("RoleClaims");
        b.Entity<IdentityUserToken<int>>().ToTable("UserTokens");

        b.Entity<ApplicationUserRole>(e =>
        {
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ApplicationUser>(e =>
        {
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Guardian).WithMany().HasForeignKey(x => x.GuardianId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.SetNull);
        });

        // ----- فهارس التفرد -----
        b.Entity<Student>().HasIndex(x => x.StudentNo).IsUnique();
        b.Entity<Student>().HasIndex(x => x.QrToken).IsUnique();
        b.Entity<Employee>().HasIndex(x => x.EmployeeNo).IsUnique();
        b.Entity<Employee>().HasIndex(x => x.QrToken).IsUnique();
        b.Entity<Subject>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Invoice>().HasIndex(x => x.InvoiceNo).IsUnique();
        b.Entity<Payment>().HasIndex(x => x.ReceiptNo).IsUnique();
        b.Entity<TransportRoute>().HasIndex(x => x.Code).IsUnique();
        b.Entity<AcademicYear>().HasIndex(x => x.Name).IsUnique();

        // لا يجوز تكرار سجل حضور لنفس الطالب في نفس اليوم
        b.Entity<StudentAttendance>().HasIndex(x => new { x.StudentId, x.Date }).IsUnique();
        b.Entity<StaffAttendance>().HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
        b.Entity<ExamResult>().HasIndex(x => new { x.ExamId, x.StudentId }).IsUnique();
        b.Entity<HomeworkSubmission>().HasIndex(x => new { x.HomeworkId, x.StudentId }).IsUnique();
        b.Entity<StudentGuardian>().HasIndex(x => new { x.StudentId, x.GuardianId }).IsUnique();
        b.Entity<Enrollment>().HasIndex(x => new { x.StudentId, x.AcademicYearId }).IsUnique();
        b.Entity<TeacherSubject>().HasIndex(x => new { x.TeacherId, x.SubjectId, x.SectionId, x.AcademicYearId }).IsUnique();
        b.Entity<TimetableSlot>().HasIndex(x => new { x.SectionId, x.DayOfWeek, x.PeriodNo, x.AcademicYearId }).IsUnique();

        // فهارس بحث شائعة
        b.Entity<StudentAttendance>().HasIndex(x => new { x.Date, x.SectionId });
        b.Entity<Notification>().HasIndex(x => new { x.UserId, x.IsRead });
        b.Entity<Payment>().HasIndex(x => x.PaymentDate);
        b.Entity<Installment>().HasIndex(x => new { x.DueDate, x.Status });
        b.Entity<Student>().HasIndex(x => new { x.Status, x.CurrentSectionId });

        // ----- علاقات تحتاج ضبطاً خاصاً -----
        b.Entity<Section>()
            .HasOne(x => x.HomeroomTeacher).WithMany()
            .HasForeignKey(x => x.HomeroomTeacherId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<Student>()
            .HasOne(x => x.CurrentSection).WithMany()
            .HasForeignKey(x => x.CurrentSectionId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<StudentTransfer>(e =>
        {
            e.HasOne(x => x.FromSection).WithMany().HasForeignKey(x => x.FromSectionId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.ToSection).WithMany().HasForeignKey(x => x.ToSectionId).OnDelete(DeleteBehavior.NoAction);
        });

        // مفتاحان إلى نفس جدول الموظفين: يجب منع المسارات المتعددة للحذف
        b.Entity<Bus>(e =>
        {
            e.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<Message>()
            .HasOne(x => x.ParentMessage).WithMany()
            .HasForeignKey(x => x.ParentMessageId).OnDelete(DeleteBehavior.NoAction);

        b.Entity<Message>()
            .HasOne(x => x.Sender).WithMany()
            .HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.NoAction);

        b.Entity<MessageRecipient>(e =>
        {
            e.HasOne(x => x.Message).WithMany(m => m.Recipients)
                .HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Recipient).WithMany()
                .HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<Notification>()
            .HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        // العناصر التابعة تُحذف مع أصلها
        b.Entity<InvoiceLine>()
            .HasOne(x => x.Invoice).WithMany(i => i.Lines)
            .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Installment>()
            .HasOne(x => x.Invoice).WithMany(i => i.Installments)
            .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Discount>()
            .HasOne(x => x.Invoice).WithMany(i => i.Discounts)
            .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Payment>(e =>
        {
            e.HasOne(x => x.Invoice).WithMany(i => i.Payments)
                .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Installment).WithMany()
                .HasForeignKey(x => x.InstallmentId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Student).WithMany()
                .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<RouteStop>()
            .HasOne(x => x.Route).WithMany(r => r.Stops)
            .HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<ExamResult>()
            .HasOne(x => x.Exam).WithMany(e => e.Results)
            .HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<HomeworkSubmission>()
            .HasOne(x => x.Homework).WithMany(h => h.Submissions)
            .HasForeignKey(x => x.HomeworkId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<StudentDocument>()
            .HasOne(x => x.Student).WithMany(s => s.Documents)
            .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<StudentGuardian>(e =>
        {
            e.HasOne(x => x.Student).WithMany(s => s.StudentGuardians)
                .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Guardian).WithMany(g => g.StudentGuardians)
                .HasForeignKey(x => x.GuardianId).OnDelete(DeleteBehavior.Cascade);
        });

        // كل بقية العلاقات: منع الحذف المتسلسل لتفادي دورات Cascade في SQL Server
        foreach (var fk in b.Model.GetEntityTypes()
                     .SelectMany(t => t.GetForeignKeys())
                     .Where(fk => !fk.IsOwnership && fk.DeleteBehavior == DeleteBehavior.Cascade))
        {
            var declaring = fk.DeclaringEntityType.ClrType;
            var isIdentityJoin = declaring == typeof(ApplicationUserRole)
                                 || declaring == typeof(IdentityUserClaim<int>)
                                 || declaring == typeof(IdentityUserLogin<int>)
                                 || declaring == typeof(IdentityUserToken<int>)
                                 || declaring == typeof(IdentityRoleClaim<int>);

            var isOwnedCollection = declaring == typeof(InvoiceLine) || declaring == typeof(Installment)
                                    || declaring == typeof(Discount) || declaring == typeof(RouteStop)
                                    || declaring == typeof(ExamResult) || declaring == typeof(HomeworkSubmission)
                                    || declaring == typeof(StudentDocument) || declaring == typeof(StudentGuardian)
                                    || declaring == typeof(MessageRecipient) || declaring == typeof(Notification);

            if (!isIdentityJoin && !isOwnedCollection)
                fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}
