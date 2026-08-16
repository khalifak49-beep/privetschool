namespace SchoolSys.Security;

/// <summary>
/// تعريف كامل لصلاحيات النظام. كل صلاحية تُخزَّن كـ Role Claim من النوع "permission"
/// ويتم التحقق منها عبر <see cref="PermissionAuthorizationHandler"/>.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    // ---------- لوحة التحكم ----------
    public const string DashboardView = "Dashboard.View";
    public const string DashboardFinance = "Dashboard.Finance";

    // ---------- الطلاب ----------
    public const string StudentsView = "Students.View";
    public const string StudentsCreate = "Students.Create";
    public const string StudentsEdit = "Students.Edit";
    public const string StudentsDelete = "Students.Delete";
    public const string StudentsTransfer = "Students.Transfer";
    public const string StudentsDocuments = "Students.Documents";
    public const string StudentsNotes = "Students.Notes";

    // ---------- أولياء الأمور ----------
    public const string GuardiansView = "Guardians.View";
    public const string GuardiansCreate = "Guardians.Create";
    public const string GuardiansEdit = "Guardians.Edit";
    public const string GuardiansDelete = "Guardians.Delete";

    // ---------- الموظفون والمعلمون ----------
    public const string EmployeesView = "Employees.View";
    public const string EmployeesCreate = "Employees.Create";
    public const string EmployeesEdit = "Employees.Edit";
    public const string EmployeesDelete = "Employees.Delete";

    // ---------- الإدارة الأكاديمية ----------
    public const string AcademicView = "Academic.View";
    public const string AcademicManage = "Academic.Manage";
    public const string AcademicAssignSubjects = "Academic.AssignSubjects";
    public const string TimetableView = "Timetable.View";
    public const string TimetableManage = "Timetable.Manage";

    // ---------- الحضور والانصراف ----------
    public const string AttendanceView = "Attendance.View";
    public const string AttendanceTakeStudents = "Attendance.TakeStudents";
    public const string AttendanceTakeStaff = "Attendance.TakeStaff";
    public const string AttendanceReports = "Attendance.Reports";

    // ---------- الاختبارات والدرجات ----------
    public const string ExamsView = "Exams.View";
    public const string ExamsManage = "Exams.Manage";
    public const string ExamsEnterMarks = "Exams.EnterMarks";
    public const string ExamsApprove = "Exams.Approve";
    public const string ResultsView = "Results.View";
    public const string ResultsCertificates = "Results.Certificates";

    // ---------- الواجبات ----------
    public const string HomeworkView = "Homework.View";
    public const string HomeworkManage = "Homework.Manage";
    public const string HomeworkGrade = "Homework.Grade";
    public const string HomeworkSubmit = "Homework.Submit";

    // ---------- الرسوم والمحاسبة ----------
    public const string FinanceView = "Finance.View";
    public const string FinanceInvoices = "Finance.Invoices";
    public const string FinancePayments = "Finance.Payments";
    public const string FinanceDiscounts = "Finance.Discounts";
    public const string FinanceFeeItems = "Finance.FeeItems";
    public const string FinanceReports = "Finance.Reports";
    public const string FinanceCancelPayment = "Finance.CancelPayment";

    // ---------- النقل المدرسي ----------
    public const string TransportView = "Transport.View";
    public const string TransportManage = "Transport.Manage";
    public const string TransportLog = "Transport.Log";

    // ---------- التواصل ----------
    public const string MessagesUse = "Messages.Use";
    public const string AnnouncementsView = "Announcements.View";
    public const string AnnouncementsManage = "Announcements.Manage";
    public const string NotificationsSend = "Notifications.Send";

    // ---------- التقارير ----------
    public const string ReportsView = "Reports.View";
    public const string ReportsExport = "Reports.Export";

    // ---------- إدارة النظام ----------
    public const string UsersView = "Users.View";
    public const string UsersManage = "Users.Manage";
    public const string RolesView = "Roles.View";
    public const string RolesManage = "Roles.Manage";
    public const string SettingsManage = "Settings.Manage";
    public const string AuditView = "Audit.View";

    /// <summary>مجموعات الصلاحيات كما تظهر في شاشة تحرير صلاحيات الأدوار.</summary>
    public static readonly IReadOnlyList<PermissionGroup> Groups = new List<PermissionGroup>
    {
        new("لوحة التحكم", "bi-speedometer2", new[]
        {
            new PermissionItem(DashboardView, "عرض لوحة التحكم"),
            new PermissionItem(DashboardFinance, "عرض المؤشرات المالية")
        }),
        new("الطلاب", "bi-mortarboard", new[]
        {
            new PermissionItem(StudentsView, "عرض الطلاب"),
            new PermissionItem(StudentsCreate, "تسجيل طالب جديد"),
            new PermissionItem(StudentsEdit, "تعديل بيانات الطالب"),
            new PermissionItem(StudentsDelete, "حذف طالب"),
            new PermissionItem(StudentsTransfer, "نقل الطالب بين الصفوف"),
            new PermissionItem(StudentsDocuments, "إدارة مستندات الطالب"),
            new PermissionItem(StudentsNotes, "إدارة الملاحظات والسلوك")
        }),
        new("أولياء الأمور", "bi-people", new[]
        {
            new PermissionItem(GuardiansView, "عرض أولياء الأمور"),
            new PermissionItem(GuardiansCreate, "إضافة ولي أمر"),
            new PermissionItem(GuardiansEdit, "تعديل ولي أمر"),
            new PermissionItem(GuardiansDelete, "حذف ولي أمر")
        }),
        new("الموظفون والمعلمون", "bi-person-badge", new[]
        {
            new PermissionItem(EmployeesView, "عرض الموظفين"),
            new PermissionItem(EmployeesCreate, "إضافة موظف"),
            new PermissionItem(EmployeesEdit, "تعديل موظف"),
            new PermissionItem(EmployeesDelete, "حذف موظف")
        }),
        new("الإدارة الأكاديمية", "bi-building", new[]
        {
            new PermissionItem(AcademicView, "عرض البنية الأكاديمية"),
            new PermissionItem(AcademicManage, "إدارة المراحل والصفوف والشعب والمواد"),
            new PermissionItem(AcademicAssignSubjects, "توزيع المواد على المعلمين"),
            new PermissionItem(TimetableView, "عرض الجداول الدراسية"),
            new PermissionItem(TimetableManage, "إعداد الجداول الدراسية")
        }),
        new("الحضور والانصراف", "bi-calendar-check", new[]
        {
            new PermissionItem(AttendanceView, "عرض سجلات الحضور"),
            new PermissionItem(AttendanceTakeStudents, "تسجيل حضور الطلاب"),
            new PermissionItem(AttendanceTakeStaff, "تسجيل حضور الموظفين"),
            new PermissionItem(AttendanceReports, "تقارير الحضور")
        }),
        new("الاختبارات والدرجات", "bi-journal-check", new[]
        {
            new PermissionItem(ExamsView, "عرض الاختبارات"),
            new PermissionItem(ExamsManage, "إنشاء وتعديل الاختبارات"),
            new PermissionItem(ExamsEnterMarks, "إدخال الدرجات"),
            new PermissionItem(ExamsApprove, "اعتماد النتائج"),
            new PermissionItem(ResultsView, "عرض النتائج وكشوف الدرجات"),
            new PermissionItem(ResultsCertificates, "طباعة الشهادات")
        }),
        new("الواجبات", "bi-pencil-square", new[]
        {
            new PermissionItem(HomeworkView, "عرض الواجبات"),
            new PermissionItem(HomeworkManage, "إنشاء وتعديل الواجبات"),
            new PermissionItem(HomeworkGrade, "تصحيح الواجبات"),
            new PermissionItem(HomeworkSubmit, "تسليم الواجبات")
        }),
        new("الرسوم والمحاسبة", "bi-cash-coin", new[]
        {
            new PermissionItem(FinanceView, "عرض الشؤون المالية"),
            new PermissionItem(FinanceFeeItems, "إدارة بنود الرسوم"),
            new PermissionItem(FinanceInvoices, "إصدار وإدارة الفواتير"),
            new PermissionItem(FinancePayments, "تسجيل المدفوعات"),
            new PermissionItem(FinanceDiscounts, "اعتماد الخصومات"),
            new PermissionItem(FinanceCancelPayment, "إلغاء سند قبض"),
            new PermissionItem(FinanceReports, "التقارير المالية")
        }),
        new("النقل المدرسي", "bi-bus-front", new[]
        {
            new PermissionItem(TransportView, "عرض النقل المدرسي"),
            new PermissionItem(TransportManage, "إدارة الحافلات وخطوط السير"),
            new PermissionItem(TransportLog, "تسجيل الصعود والنزول")
        }),
        new("التواصل والإشعارات", "bi-chat-dots", new[]
        {
            new PermissionItem(MessagesUse, "استخدام الرسائل الداخلية"),
            new PermissionItem(AnnouncementsView, "عرض الإعلانات"),
            new PermissionItem(AnnouncementsManage, "إدارة الإعلانات"),
            new PermissionItem(NotificationsSend, "إرسال إشعارات ورسائل خارجية")
        }),
        new("التقارير", "bi-file-earmark-bar-graph", new[]
        {
            new PermissionItem(ReportsView, "عرض التقارير"),
            new PermissionItem(ReportsExport, "تصدير PDF و Excel")
        }),
        new("إدارة النظام", "bi-gear", new[]
        {
            new PermissionItem(UsersView, "عرض المستخدمين"),
            new PermissionItem(UsersManage, "إدارة المستخدمين"),
            new PermissionItem(RolesView, "عرض الأدوار"),
            new PermissionItem(RolesManage, "إدارة الأدوار والصلاحيات"),
            new PermissionItem(SettingsManage, "إعدادات النظام"),
            new PermissionItem(AuditView, "سجل التدقيق")
        })
    };

    public static IEnumerable<string> AllPermissions =>
        Groups.SelectMany(g => g.Items).Select(i => i.Value);

    public static string DisplayName(string value) =>
        Groups.SelectMany(g => g.Items).FirstOrDefault(i => i.Value == value)?.Name ?? value;
}

public record PermissionGroup(string Name, string Icon, IReadOnlyList<PermissionItem> Items);

public record PermissionItem(string Value, string Name);

/// <summary>أسماء الأدوار الأساسية في النظام.</summary>
public static class RoleNames
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Principal = "Principal";
    public const string VicePrincipal = "VicePrincipal";
    public const string AcademicAdmin = "AcademicAdmin";
    public const string Accountant = "Accountant";
    public const string Teacher = "Teacher";
    public const string Student = "Student";
    public const string Guardian = "Guardian";
    public const string TransportManager = "TransportManager";
    public const string Receptionist = "Receptionist";

    public static readonly Dictionary<string, string> Arabic = new()
    {
        [SuperAdmin] = "مسؤول النظام",
        [Principal] = "مدير المدرسة",
        [VicePrincipal] = "نائب المدير",
        [AcademicAdmin] = "الإدارة الأكاديمية",
        [Accountant] = "المحاسب",
        [Teacher] = "المعلم",
        [Student] = "الطالب",
        [Guardian] = "ولي الأمر",
        [TransportManager] = "مسؤول النقل",
        [Receptionist] = "موظف الاستقبال"
    };

    public static string Display(string role) => Arabic.TryGetValue(role, out var v) ? v : role;
}
