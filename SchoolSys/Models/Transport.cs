using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolSys.Models;

/// <summary>حافلة مدرسية</summary>
public class Bus
{
    public int Id { get; set; }

    [Required, StringLength(30), Display(Name = "رقم الحافلة")]
    public string BusNo { get; set; } = string.Empty;

    [Required, StringLength(30), Display(Name = "رقم اللوحة")]
    public string PlateNo { get; set; } = string.Empty;

    [Display(Name = "السعة")]
    public int Capacity { get; set; } = 30;

    [StringLength(80), Display(Name = "الموديل")]
    public string? Model { get; set; }

    [Display(Name = "سنة الصنع")]
    public int? ManufactureYear { get; set; }

    [Display(Name = "السائق")]
    public int? DriverId { get; set; }
    public Employee? Driver { get; set; }

    [Display(Name = "المشرف")]
    public int? SupervisorId { get; set; }
    public Employee? Supervisor { get; set; }

    [Display(Name = "تاريخ انتهاء الترخيص"), DataType(DataType.Date)]
    public DateTime? LicenseExpiry { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    public ICollection<TransportRoute> Routes { get; set; } = new List<TransportRoute>();
}

/// <summary>خط سير</summary>
public class TransportRoute
{
    public int Id { get; set; }

    [Required, StringLength(30), Display(Name = "رمز الخط")]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(150), Display(Name = "اسم الخط")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "الحافلة")]
    public int? BusId { get; set; }
    public Bus? Bus { get; set; }

    [StringLength(500), Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Display(Name = "الرسوم الشهرية"), Column(TypeName = "decimal(18,2)")]
    public decimal MonthlyFee { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    public ICollection<RouteStop> Stops { get; set; } = new List<RouteStop>();
    public ICollection<StudentTransport> Subscriptions { get; set; } = new List<StudentTransport>();
}

/// <summary>محطة على خط السير</summary>
public class RouteStop
{
    public int Id { get; set; }

    [Display(Name = "خط السير")]
    public int RouteId { get; set; }
    public TransportRoute Route { get; set; } = null!;

    [Required, StringLength(150), Display(Name = "اسم المحطة")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "الترتيب")]
    public int SeqNo { get; set; }

    [Display(Name = "وقت الصعود")]
    public TimeSpan? PickupTime { get; set; }

    [Display(Name = "وقت النزول")]
    public TimeSpan? DropTime { get; set; }

    [Column(TypeName = "decimal(10,7)"), Display(Name = "خط العرض")]
    public decimal? Latitude { get; set; }

    [Column(TypeName = "decimal(10,7)"), Display(Name = "خط الطول")]
    public decimal? Longitude { get; set; }
}

/// <summary>اشتراك الطالب في النقل المدرسي</summary>
public class StudentTransport
{
    public int Id { get; set; }

    [Display(Name = "الطالب")]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Display(Name = "خط السير")]
    public int RouteId { get; set; }
    public TransportRoute Route { get; set; } = null!;

    [Display(Name = "المحطة")]
    public int? StopId { get; set; }
    public RouteStop? Stop { get; set; }

    [Display(Name = "العام الدراسي")]
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    [Display(Name = "الرسوم الشهرية"), Column(TypeName = "decimal(18,2)")]
    public decimal MonthlyFee { get; set; }

    [Display(Name = "تاريخ البداية"), DataType(DataType.Date)]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Display(Name = "تاريخ النهاية"), DataType(DataType.Date)]
    public DateTime? EndDate { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;
}

/// <summary>سجل صعود / نزول الطالب من الحافلة</summary>
public class TransportLog
{
    public int Id { get; set; }

    [Display(Name = "الطالب")]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Display(Name = "خط السير")]
    public int RouteId { get; set; }
    public TransportRoute Route { get; set; } = null!;

    [Display(Name = "الحافلة")]
    public int? BusId { get; set; }
    public Bus? Bus { get; set; }

    [Display(Name = "التاريخ"), DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [Display(Name = "الوقت")]
    public TimeSpan Time { get; set; }

    [Display(Name = "الحدث")]
    public TransportEvent EventType { get; set; } = TransportEvent.Board;

    [Display(Name = "الاتجاه")]
    public TransportDirection Direction { get; set; } = TransportDirection.ToSchool;

    [StringLength(300), Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    public int? RecordedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
