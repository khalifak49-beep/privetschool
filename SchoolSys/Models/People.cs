using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolSys.Models;

/// <summary>الطالب</summary>
public class Student
{
    public int Id { get; set; }

    [Required, StringLength(30), Display(Name = "الرقم الطلابي")]
    public string StudentNo { get; set; } = string.Empty;

    [Required, StringLength(150), Display(Name = "اسم الطالب")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(150), Display(Name = "الاسم بالإنجليزية")]
    public string? FullNameEn { get; set; }

    [StringLength(30), Display(Name = "رقم الهوية")]
    public string? NationalId { get; set; }

    [Display(Name = "الجنس")]
    public Gender Gender { get; set; } = Gender.Male;

    [Display(Name = "تاريخ الميلاد"), DataType(DataType.Date)]
    public DateTime? BirthDate { get; set; }

    [StringLength(100), Display(Name = "مكان الميلاد")]
    public string? BirthPlace { get; set; }

    [StringLength(60), Display(Name = "الجنسية")]
    public string? Nationality { get; set; }

    [StringLength(60), Display(Name = "الديانة")]
    public string? Religion { get; set; }

    [StringLength(300), Display(Name = "العنوان")]
    public string? Address { get; set; }

    [StringLength(30), Display(Name = "الهاتف")]
    public string? Phone { get; set; }

    [StringLength(150), Display(Name = "البريد الإلكتروني")]
    public string? Email { get; set; }

    [StringLength(300), Display(Name = "الصورة")]
    public string? PhotoPath { get; set; }

    [Display(Name = "تاريخ الالتحاق"), DataType(DataType.Date)]
    public DateTime EnrollmentDate { get; set; } = DateTime.Today;

    [Display(Name = "الحالة")]
    public StudentStatus Status { get; set; } = StudentStatus.Active;

    [Display(Name = "الشعبة الحالية")]
    public int? CurrentSectionId { get; set; }
    public Section? CurrentSection { get; set; }

    [StringLength(10), Display(Name = "فصيلة الدم")]
    public string? BloodType { get; set; }

    [StringLength(500), Display(Name = "ملاحظات صحية")]
    public string? HealthNotes { get; set; }

    [StringLength(150), Display(Name = "المدرسة السابقة")]
    public string? PreviousSchool { get; set; }

    /// <summary>رمز فريد يُستخدم لتوليد بطاقة QR للحضور.</summary>
    [StringLength(64)]
    public string QrToken { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [NotMapped, Display(Name = "العمر")]
    public int? Age => BirthDate.HasValue
        ? (int)((DateTime.Today - BirthDate.Value).TotalDays / 365.2425)
        : null;

    public ICollection<StudentGuardian> StudentGuardians { get; set; } = new List<StudentGuardian>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<StudentDocument> Documents { get; set; } = new List<StudentDocument>();
    public ICollection<StudentNote> Notes { get; set; } = new List<StudentNote>();
    public ICollection<StudentAttendance> Attendances { get; set; } = new List<StudentAttendance>();
    public ICollection<ExamResult> ExamResults { get; set; } = new List<ExamResult>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

/// <summary>ولي الأمر</summary>
public class Guardian
{
    public int Id { get; set; }

    [Required, StringLength(150), Display(Name = "اسم ولي الأمر")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(30), Display(Name = "رقم الهوية")]
    public string? NationalId { get; set; }

    [Required, StringLength(30), Display(Name = "رقم الجوال")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(30), Display(Name = "هاتف بديل")]
    public string? AltPhone { get; set; }

    [StringLength(150), Display(Name = "البريد الإلكتروني")]
    public string? Email { get; set; }

    [StringLength(100), Display(Name = "المهنة")]
    public string? Job { get; set; }

    [StringLength(150), Display(Name = "جهة العمل")]
    public string? Workplace { get; set; }

    [StringLength(300), Display(Name = "العنوان")]
    public string? Address { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<StudentGuardian> StudentGuardians { get; set; } = new List<StudentGuardian>();
}

/// <summary>ربط ولي الأمر بأبنائه</summary>
public class StudentGuardian
{
    public int Id { get; set; }

    [Display(Name = "الطالب")]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Display(Name = "ولي الأمر")]
    public int GuardianId { get; set; }
    public Guardian Guardian { get; set; } = null!;

    [Required, StringLength(50), Display(Name = "صلة القرابة")]
    public string Relation { get; set; } = "الأب";

    [Display(Name = "ولي الأمر الأساسي")]
    public bool IsPrimary { get; set; }

    [Display(Name = "مصرّح باستلام الطالب")]
    public bool CanPickup { get; set; } = true;
}

/// <summary>الموظف (معلم / إداري / محاسب / سائق ...)</summary>
public class Employee
{
    public int Id { get; set; }

    [Required, StringLength(30), Display(Name = "الرقم الوظيفي")]
    public string EmployeeNo { get; set; } = string.Empty;

    [Required, StringLength(150), Display(Name = "الاسم")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "الوظيفة")]
    public EmployeeType EmployeeType { get; set; } = EmployeeType.Teacher;

    [StringLength(30), Display(Name = "رقم الهوية")]
    public string? NationalId { get; set; }

    [Display(Name = "الجنس")]
    public Gender Gender { get; set; } = Gender.Male;

    [Display(Name = "تاريخ الميلاد"), DataType(DataType.Date)]
    public DateTime? BirthDate { get; set; }

    [StringLength(30), Display(Name = "الجوال")]
    public string? Phone { get; set; }

    [StringLength(150), Display(Name = "البريد الإلكتروني")]
    public string? Email { get; set; }

    [StringLength(300), Display(Name = "العنوان")]
    public string? Address { get; set; }

    [Display(Name = "تاريخ التعيين"), DataType(DataType.Date)]
    public DateTime HireDate { get; set; } = DateTime.Today;

    [StringLength(150), Display(Name = "التخصص")]
    public string? Specialization { get; set; }

    [StringLength(150), Display(Name = "المؤهل العلمي")]
    public string? Qualification { get; set; }

    [Display(Name = "الراتب"), Column(TypeName = "decimal(18,2)")]
    public decimal? Salary { get; set; }

    [StringLength(300), Display(Name = "الصورة")]
    public string? PhotoPath { get; set; }

    [StringLength(64)]
    public string QrToken { get; set; } = Guid.NewGuid().ToString("N");

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [NotMapped]
    public bool IsTeacher => EmployeeType == EmployeeType.Teacher;

    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
    public ICollection<StaffAttendance> Attendances { get; set; } = new List<StaffAttendance>();
}

/// <summary>تسجيل الطالب في شعبة خلال عام دراسي</summary>
public class Enrollment
{
    public int Id { get; set; }

    [Display(Name = "الطالب")]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Display(Name = "الشعبة")]
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    [Display(Name = "العام الدراسي")]
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    [Display(Name = "تاريخ التسجيل"), DataType(DataType.Date)]
    public DateTime EnrollDate { get; set; } = DateTime.Today;

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    [StringLength(400), Display(Name = "ملاحظات")]
    public string? Notes { get; set; }
}

/// <summary>سجل نقل الطالب بين الشعب / الصفوف</summary>
public class StudentTransfer
{
    public int Id { get; set; }

    [Display(Name = "الطالب")]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Display(Name = "من شعبة")]
    public int? FromSectionId { get; set; }
    public Section? FromSection { get; set; }

    [Display(Name = "إلى شعبة")]
    public int ToSectionId { get; set; }
    public Section ToSection { get; set; } = null!;

    [Display(Name = "تاريخ النقل"), DataType(DataType.Date)]
    public DateTime TransferDate { get; set; } = DateTime.Today;

    [StringLength(400), Display(Name = "السبب")]
    public string? Reason { get; set; }

    public int? PerformedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>مستند / مرفق للطالب</summary>
public class StudentDocument
{
    public int Id { get; set; }

    [Display(Name = "الطالب")]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Required, StringLength(200), Display(Name = "عنوان المستند")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "نوع المستند")]
    public DocumentType DocType { get; set; } = DocumentType.Other;

    [Required, StringLength(400), Display(Name = "الملف")]
    public string FilePath { get; set; } = string.Empty;

    [StringLength(120)]
    public string? ContentType { get; set; }

    public long SizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.Now;
    public int? UploadedByUserId { get; set; }
}

/// <summary>ملاحظة سلوكية / أكاديمية على الطالب</summary>
public class StudentNote
{
    public int Id { get; set; }

    [Display(Name = "الطالب")]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Display(Name = "النوع")]
    public NoteType NoteType { get; set; } = NoteType.General;

    [Display(Name = "الدرجة")]
    public NoteSeverity Severity { get; set; } = NoteSeverity.Info;

    [Required, StringLength(200), Display(Name = "العنوان")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000), Display(Name = "التفاصيل")]
    public string? Body { get; set; }

    [StringLength(500), Display(Name = "الإجراء المتخذ")]
    public string? ActionTaken { get; set; }

    [Display(Name = "التاريخ"), DataType(DataType.Date)]
    public DateTime NoteDate { get; set; } = DateTime.Today;

    [Display(Name = "النقاط")]
    public int Points { get; set; }

    [Display(Name = "بواسطة")]
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    [Display(Name = "إشعار ولي الأمر")]
    public bool NotifyGuardian { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
