using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolSys.Models;

namespace SchoolSys.ViewModels;

public class BusIndexViewModel
{
    public List<BusRow> Buses { get; set; } = [];
    public List<SelectListItem> Drivers { get; set; } = [];
    public List<SelectListItem> Supervisors { get; set; } = [];
    public int TotalBuses { get; set; }
    public int TotalCapacity { get; set; }
    public int TotalSubscribers { get; set; }
}

public class BusRow
{
    public int Id { get; set; }
    public string BusNo { get; set; } = "";
    public string PlateNo { get; set; } = "";
    public int Capacity { get; set; }
    public string? Model { get; set; }
    public int? ManufactureYear { get; set; }
    public int? DriverId { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public int? SupervisorId { get; set; }
    public string? SupervisorName { get; set; }
    public DateTime? LicenseExpiry { get; set; }
    public bool IsActive { get; set; }
    public int RoutesCount { get; set; }
    public int Subscribers { get; set; }
    public double FillRate => Capacity > 0 ? (double)Subscribers / Capacity * 100 : 0;
    public bool LicenseExpiringSoon => LicenseExpiry.HasValue && LicenseExpiry.Value <= DateTime.Today.AddDays(60);
}

public class RouteIndexViewModel
{
    public List<RouteRow> Routes { get; set; } = [];
    public List<SelectListItem> Buses { get; set; } = [];
    public string Currency { get; set; } = "ر.ع";
}

public class RouteRow
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int? BusId { get; set; }
    public string? BusNo { get; set; }
    public string? DriverName { get; set; }
    public string? Description { get; set; }
    public decimal MonthlyFee { get; set; }
    public bool IsActive { get; set; }
    public int StopsCount { get; set; }
    public int Subscribers { get; set; }
    public List<RouteStop> Stops { get; set; } = [];
}

public class SubscriptionIndexViewModel
{
    public int? RouteId { get; set; }
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    public string Currency { get; set; } = "ر.ع";
    public PagedList<SubscriptionRow> Subscriptions { get; set; } = PagedList<SubscriptionRow>.Empty();
    public List<SelectListItem> Routes { get; set; } = [];
    public List<SelectListItem> Stops { get; set; } = [];
    public int TotalSubscribers { get; set; }
    public decimal MonthlyRevenue { get; set; }
}

public class SubscriptionRow
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = "";
    public string StudentNo { get; set; } = "";
    public string? Section { get; set; }
    public string RouteName { get; set; } = "";
    public int RouteId { get; set; }
    public string? StopName { get; set; }
    public string? BusNo { get; set; }
    public decimal MonthlyFee { get; set; }
    public DateTime StartDate { get; set; }
    public bool IsActive { get; set; }
    public string? GuardianPhone { get; set; }
}

public class TransportLogViewModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    public int? RouteId { get; set; }
    public TransportDirection Direction { get; set; } = TransportDirection.ToSchool;
    public List<SelectListItem> Routes { get; set; } = [];
    public List<TransportLogRow> Students { get; set; } = [];
    public string? RouteName { get; set; }
    public int BoardedCount => Students.Count(s => s.Boarded);
    public int AlightedCount => Students.Count(s => s.Alighted);
}

public class TransportLogRow
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = "";
    public string StudentNo { get; set; } = "";
    public string? PhotoPath { get; set; }
    public string? Section { get; set; }
    public string? StopName { get; set; }
    public bool Boarded { get; set; }
    public bool Alighted { get; set; }
    public TimeSpan? BoardTime { get; set; }
    public TimeSpan? AlightTime { get; set; }
    public string? GuardianPhone { get; set; }
}
