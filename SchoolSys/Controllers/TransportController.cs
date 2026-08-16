using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[HasPermission(Permissions.TransportView)]
public class TransportController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notify;
    private readonly ICurrentUserService _user;
    private readonly IAuditService _audit;

    public TransportController(ApplicationDbContext db, INotificationService notify,
        ICurrentUserService user, IAuditService audit)
    {
        _db = db;
        _notify = notify;
        _user = user;
        _audit = audit;
    }

    // ==================================================================
    // الحافلات
    // ==================================================================
    public async Task<IActionResult> Index()
    {
        var vm = new BusIndexViewModel();

        vm.Buses = await _db.Buses.AsNoTracking()
            .OrderBy(b => b.BusNo)
            .Select(b => new BusRow
            {
                Id = b.Id,
                BusNo = b.BusNo,
                PlateNo = b.PlateNo,
                Capacity = b.Capacity,
                Model = b.Model,
                ManufactureYear = b.ManufactureYear,
                DriverId = b.DriverId,
                DriverName = b.Driver != null ? b.Driver.FullName : null,
                DriverPhone = b.Driver != null ? b.Driver.Phone : null,
                SupervisorId = b.SupervisorId,
                SupervisorName = b.Supervisor != null ? b.Supervisor.FullName : null,
                LicenseExpiry = b.LicenseExpiry,
                IsActive = b.IsActive,
                RoutesCount = b.Routes.Count,
                Subscribers = b.Routes.SelectMany(r => r.Subscriptions).Count(s => s.IsActive)
            })
            .ToListAsync();

        vm.TotalBuses = vm.Buses.Count;
        vm.TotalCapacity = vm.Buses.Sum(b => b.Capacity);
        vm.TotalSubscribers = vm.Buses.Sum(b => b.Subscribers);

        vm.Drivers = await _db.Employees.AsNoTracking()
            .Where(e => e.EmployeeType == EmployeeType.Driver && e.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new SelectListItem(e.FullName, e.Id.ToString())).ToListAsync();

        vm.Supervisors = await _db.Employees.AsNoTracking()
            .Where(e => e.EmployeeType == EmployeeType.BusSupervisor && e.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new SelectListItem(e.FullName, e.Id.ToString())).ToListAsync();

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.TransportManage)]
    public async Task<IActionResult> SaveBus(Bus bus)
    {
        if (string.IsNullOrWhiteSpace(bus.BusNo) || string.IsNullOrWhiteSpace(bus.PlateNo))
        {
            Error("الرجاء إدخال رقم الحافلة ورقم اللوحة.");
            return RedirectToAction(nameof(Index));
        }

        if (bus.DriverId == 0) bus.DriverId = null;
        if (bus.SupervisorId == 0) bus.SupervisorId = null;

        if (bus.Id == 0) _db.Buses.Add(bus);
        else
        {
            var b = await _db.Buses.FindAsync(bus.Id);
            if (b is null) return NotFound();
            b.BusNo = bus.BusNo;
            b.PlateNo = bus.PlateNo;
            b.Capacity = bus.Capacity;
            b.Model = bus.Model;
            b.ManufactureYear = bus.ManufactureYear;
            b.DriverId = bus.DriverId;
            b.SupervisorId = bus.SupervisorId;
            b.LicenseExpiry = bus.LicenseExpiry;
            b.IsActive = bus.IsActive;
        }

        await _db.SaveChangesAsync();
        Success("تم حفظ بيانات الحافلة.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.TransportManage)]
    public async Task<IActionResult> DeleteBus(int id)
    {
        if (await _db.TransportRoutes.AnyAsync(r => r.BusId == id))
        {
            Error("لا يمكن حذف الحافلة لارتباطها بخطوط سير.");
            return RedirectToAction(nameof(Index));
        }

        var b = await _db.Buses.FindAsync(id);
        if (b is not null)
        {
            _db.Buses.Remove(b);
            await _db.SaveChangesAsync();
            Success("تم حذف الحافلة.");
        }
        return RedirectToAction(nameof(Index));
    }

    // ==================================================================
    // خطوط السير والمحطات
    // ==================================================================
    public async Task<IActionResult> Routes()
    {
        var settings = await GetSettingsAsync();
        var vm = new RouteIndexViewModel { Currency = settings.Currency };

        vm.Routes = await _db.TransportRoutes.AsNoTracking()
            .Include(r => r.Stops)
            .OrderBy(r => r.Code)
            .Select(r => new RouteRow
            {
                Id = r.Id,
                Code = r.Code,
                Name = r.Name,
                BusId = r.BusId,
                BusNo = r.Bus != null ? r.Bus.BusNo : null,
                DriverName = r.Bus != null && r.Bus.Driver != null ? r.Bus.Driver.FullName : null,
                Description = r.Description,
                MonthlyFee = r.MonthlyFee,
                IsActive = r.IsActive,
                StopsCount = r.Stops.Count,
                Subscribers = r.Subscriptions.Count(s => s.IsActive),
                Stops = r.Stops.OrderBy(s => s.SeqNo).ToList()
            })
            .ToListAsync();

        vm.Buses = await _db.Buses.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BusNo)
            .Select(b => new SelectListItem($"{b.BusNo} — {b.PlateNo}", b.Id.ToString())).ToListAsync();

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.TransportManage)]
    public async Task<IActionResult> SaveRoute(TransportRoute model)
    {
        if (string.IsNullOrWhiteSpace(model.Code) || string.IsNullOrWhiteSpace(model.Name))
        {
            Error("الرجاء إدخال رمز الخط واسمه.");
            return RedirectToAction(nameof(Routes));
        }

        if (model.BusId == 0) model.BusId = null;

        if (model.Id == 0)
        {
            if (await _db.TransportRoutes.AnyAsync(r => r.Code == model.Code))
            {
                Error("رمز الخط مستخدم مسبقاً.");
                return RedirectToAction(nameof(Routes));
            }
            _db.TransportRoutes.Add(model);
        }
        else
        {
            var r = await _db.TransportRoutes.FindAsync(model.Id);
            if (r is null) return NotFound();
            r.Code = model.Code;
            r.Name = model.Name;
            r.BusId = model.BusId;
            r.Description = model.Description;
            r.MonthlyFee = model.MonthlyFee;
            r.IsActive = model.IsActive;
        }

        await _db.SaveChangesAsync();
        Success("تم حفظ خط السير.");
        return RedirectToAction(nameof(Routes));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.TransportManage)]
    public async Task<IActionResult> DeleteRoute(int id)
    {
        if (await _db.StudentTransports.AnyAsync(s => s.RouteId == id))
        {
            Error("لا يمكن حذف الخط لوجود طلاب مشتركين به.");
            return RedirectToAction(nameof(Routes));
        }

        var r = await _db.TransportRoutes.FindAsync(id);
        if (r is not null)
        {
            _db.TransportRoutes.Remove(r);   // المحطات تُحذف تلقائياً
            await _db.SaveChangesAsync();
            Success("تم حذف خط السير.");
        }
        return RedirectToAction(nameof(Routes));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.TransportManage)]
    public async Task<IActionResult> SaveStop(RouteStop model)
    {
        if (string.IsNullOrWhiteSpace(model.Name) || model.RouteId == 0)
        {
            Error("الرجاء إدخال اسم المحطة واختيار الخط.");
            return RedirectToAction(nameof(Routes));
        }

        if (model.Id == 0) _db.RouteStops.Add(model);
        else
        {
            var s = await _db.RouteStops.FindAsync(model.Id);
            if (s is null) return NotFound();
            s.Name = model.Name;
            s.SeqNo = model.SeqNo;
            s.PickupTime = model.PickupTime;
            s.DropTime = model.DropTime;
        }

        await _db.SaveChangesAsync();
        Success("تم حفظ المحطة.");
        return RedirectToAction(nameof(Routes));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.TransportManage)]
    public async Task<IActionResult> DeleteStop(int id)
    {
        var s = await _db.RouteStops.FindAsync(id);
        if (s is null) return NotFound();

        var inUse = await _db.StudentTransports.AnyAsync(t => t.StopId == id);
        if (inUse)
        {
            Error("لا يمكن حذف المحطة لارتباطها باشتراكات طلاب.");
            return RedirectToAction(nameof(Routes));
        }

        _db.RouteStops.Remove(s);
        await _db.SaveChangesAsync();
        Success("تم حذف المحطة.");
        return RedirectToAction(nameof(Routes));
    }

    // ==================================================================
    // اشتراكات الطلاب
    // ==================================================================
    public async Task<IActionResult> Subscriptions(int? routeId, string? q, int page = 1)
    {
        var settings = await GetSettingsAsync();
        var year = await GetCurrentYearAsync();

        var vm = new SubscriptionIndexViewModel
        {
            RouteId = routeId,
            Q = q,
            Page = page,
            Currency = settings.Currency
        };

        var query = _db.StudentTransports.AsNoTracking()
            .Where(t => year == null || t.AcademicYearId == year.Id);

        if (routeId.HasValue) query = query.Where(t => t.RouteId == routeId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(t => t.Student.FullName.Contains(term) || t.Student.StudentNo.Contains(term));
        }

        var projected = query.Select(t => new SubscriptionRow
        {
            Id = t.Id,
            StudentId = t.StudentId,
            StudentName = t.Student.FullName,
            StudentNo = t.Student.StudentNo,
            Section = t.Student.CurrentSection != null
                ? t.Student.CurrentSection.Grade.Name + " - " + t.Student.CurrentSection.Name : null,
            RouteId = t.RouteId,
            RouteName = t.Route.Name,
            StopName = t.Stop != null ? t.Stop.Name : null,
            BusNo = t.Route.Bus != null ? t.Route.Bus.BusNo : null,
            MonthlyFee = t.MonthlyFee,
            StartDate = t.StartDate,
            IsActive = t.IsActive,
            GuardianPhone = t.Student.StudentGuardians
                .OrderByDescending(sg => sg.IsPrimary).Select(sg => sg.Guardian.Phone).FirstOrDefault()
        });

        vm.Subscriptions = await PagedList<SubscriptionRow>.CreateAsync(
            projected.OrderBy(s => s.RouteName).ThenBy(s => s.StudentName), page, 25);

        vm.TotalSubscribers = await query.CountAsync(t => t.IsActive);
        vm.MonthlyRevenue = await query.Where(t => t.IsActive).SumAsync(t => (decimal?)t.MonthlyFee) ?? 0m;

        vm.Routes = await _db.TransportRoutes.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.Code)
            .Select(r => new SelectListItem(r.Code + " — " + r.Name, r.Id.ToString(), r.Id == routeId))
            .ToListAsync();

        vm.Stops = await _db.RouteStops.AsNoTracking().OrderBy(s => s.Route.Code).ThenBy(s => s.SeqNo)
            .Select(s => new SelectListItem(s.Route.Name + " / " + s.Name, s.Id.ToString()))
            .ToListAsync();

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.TransportManage)]
    public async Task<IActionResult> SaveSubscription(int id, int studentId, int routeId, int? stopId,
        decimal monthlyFee, DateTime startDate, bool isActive)
    {
        var year = await GetCurrentYearAsync();
        if (year is null)
        {
            Error("لا يوجد عام دراسي محدد.");
            return RedirectToAction(nameof(Subscriptions));
        }

        if (id == 0)
        {
            var exists = await _db.StudentTransports
                .AnyAsync(t => t.StudentId == studentId && t.AcademicYearId == year.Id && t.IsActive);

            if (exists)
            {
                Error("الطالب مشترك بالفعل في النقل المدرسي لهذا العام.");
                return RedirectToAction(nameof(Subscriptions), new { routeId });
            }

            _db.StudentTransports.Add(new StudentTransport
            {
                StudentId = studentId,
                RouteId = routeId,
                StopId = stopId == 0 ? null : stopId,
                AcademicYearId = year.Id,
                MonthlyFee = monthlyFee,
                StartDate = startDate,
                IsActive = isActive
            });
        }
        else
        {
            var t = await _db.StudentTransports.FindAsync(id);
            if (t is null) return NotFound();
            t.RouteId = routeId;
            t.StopId = stopId == 0 ? null : stopId;
            t.MonthlyFee = monthlyFee;
            t.StartDate = startDate;
            t.IsActive = isActive;
        }

        await _db.SaveChangesAsync();
        Success("تم حفظ اشتراك النقل.");
        return RedirectToAction(nameof(Subscriptions), new { routeId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.TransportManage)]
    public async Task<IActionResult> DeleteSubscription(int id, int? routeId)
    {
        var t = await _db.StudentTransports.FindAsync(id);
        if (t is not null)
        {
            _db.StudentTransports.Remove(t);
            await _db.SaveChangesAsync();
            Success("تم إلغاء الاشتراك.");
        }
        return RedirectToAction(nameof(Subscriptions), new { routeId });
    }

    // ==================================================================
    // سجل الصعود والنزول
    // ==================================================================
    [HasPermission(Permissions.TransportLog)]
    public async Task<IActionResult> Logs(int? routeId, DateTime? date, TransportDirection direction = TransportDirection.ToSchool)
    {
        var year = await GetCurrentYearAsync();
        var day = (date ?? DateTime.Today).Date;

        var vm = new TransportLogViewModel
        {
            RouteId = routeId,
            Date = day,
            Direction = direction
        };

        vm.Routes = await _db.TransportRoutes.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.Code)
            .Select(r => new SelectListItem(r.Code + " — " + r.Name, r.Id.ToString(), r.Id == routeId))
            .ToListAsync();

        if (routeId is null && vm.Routes.Count > 0)
        {
            routeId = int.Parse(vm.Routes[0].Value);
            vm.RouteId = routeId;
            vm.Routes[0].Selected = true;
        }

        if (routeId is null) return View(vm);

        vm.RouteName = await _db.TransportRoutes.Where(r => r.Id == routeId).Select(r => r.Name).FirstOrDefaultAsync();

        var subs = await _db.StudentTransports.AsNoTracking()
            .Where(t => t.RouteId == routeId && t.IsActive && (year == null || t.AcademicYearId == year.Id))
            .Select(t => new
            {
                t.StudentId,
                t.Student.FullName,
                t.Student.StudentNo,
                t.Student.PhotoPath,
                Section = t.Student.CurrentSection != null
                    ? t.Student.CurrentSection.Grade.Name + " - " + t.Student.CurrentSection.Name : null,
                StopName = t.Stop != null ? t.Stop.Name : null,
                GuardianPhone = t.Student.StudentGuardians
                    .OrderByDescending(sg => sg.IsPrimary).Select(sg => sg.Guardian.Phone).FirstOrDefault()
            })
            .ToListAsync();

        var studentIds = subs.Select(s => s.StudentId).ToList();
        var logs = await _db.TransportLogs.AsNoTracking()
            .Where(l => l.Date == day && l.RouteId == routeId && l.Direction == direction &&
                        studentIds.Contains(l.StudentId))
            .ToListAsync();

        vm.Students = subs.Select(s =>
        {
            var board = logs.FirstOrDefault(l => l.StudentId == s.StudentId && l.EventType == TransportEvent.Board);
            var alight = logs.FirstOrDefault(l => l.StudentId == s.StudentId && l.EventType == TransportEvent.Alight);

            return new TransportLogRow
            {
                StudentId = s.StudentId,
                StudentName = s.FullName,
                StudentNo = s.StudentNo,
                PhotoPath = s.PhotoPath,
                Section = s.Section,
                StopName = s.StopName,
                GuardianPhone = s.GuardianPhone,
                Boarded = board is not null,
                BoardTime = board?.Time,
                Alighted = alight is not null,
                AlightTime = alight?.Time
            };
        })
        .OrderBy(s => s.StopName).ThenBy(s => s.StudentName)
        .ToList();

        return View(vm);
    }

    [HttpPost, HasPermission(Permissions.TransportLog)]
    public async Task<IActionResult> RecordEvent([FromBody] TransportEventRequest request)
    {
        var year = await GetCurrentYearAsync();
        var day = request.Date.Date;

        var student = await _db.Students
            .Where(s => s.Id == request.StudentId)
            .Select(s => new { s.Id, s.FullName })
            .FirstOrDefaultAsync();

        if (student is null) return Json(new { success = false, message = "لم يتم العثور على الطالب." });

        var existing = await _db.TransportLogs.FirstOrDefaultAsync(l =>
            l.StudentId == request.StudentId && l.Date == day &&
            l.RouteId == request.RouteId && l.Direction == request.Direction &&
            l.EventType == request.EventType);

        var now = DateTime.Now.TimeOfDay;

        if (existing is not null)
        {
            // إلغاء التسجيل عند الضغط مرة أخرى
            _db.TransportLogs.Remove(existing);
            await _db.SaveChangesAsync();
            return Json(new { success = true, recorded = false, message = "تم إلغاء التسجيل" });
        }

        var busId = await _db.TransportRoutes.Where(r => r.Id == request.RouteId)
            .Select(r => r.BusId).FirstOrDefaultAsync();

        _db.TransportLogs.Add(new TransportLog
        {
            StudentId = request.StudentId,
            RouteId = request.RouteId,
            BusId = busId,
            Date = day,
            Time = now,
            EventType = request.EventType,
            Direction = request.Direction,
            RecordedByUserId = _user.UserId
        });

        await _db.SaveChangesAsync();

        // إشعار ولي الأمر
        if (request.NotifyGuardian)
        {
            var action = request.EventType == TransportEvent.Board ? "صعد إلى" : "نزل من";
            var place = request.Direction == TransportDirection.ToSchool ? "الحافلة متجهاً إلى المدرسة" : "الحافلة عائداً إلى المنزل";

            await _notify.NotifyGuardiansOfStudentAsync(request.StudentId,
                "النقل المدرسي",
                $"الطالب {student.FullName} {action} {place} الساعة {DateTime.Now:hh:mm tt}.",
                NotificationType.Transport, NotificationSeverity.Info, alsoExternal: false);
        }

        return Json(new
        {
            success = true,
            recorded = true,
            time = DateTime.Today.Add(now).ToString("hh:mm tt"),
            message = "تم التسجيل"
        });
    }

    public record TransportEventRequest(int StudentId, int RouteId, DateTime Date,
        TransportEvent EventType, TransportDirection Direction, bool NotifyGuardian);

    /// <summary>بحث سريع عن طالب لإضافة اشتراك نقل.</summary>
    [HttpGet]
    public async Task<IActionResult> SearchStudents(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Json(Array.Empty<object>());

        var term = q.Trim();
        var items = await _db.Students.AsNoTracking()
            .Where(s => s.Status == StudentStatus.Active &&
                        (s.FullName.Contains(term) || s.StudentNo.Contains(term)))
            .OrderBy(s => s.FullName)
            .Take(12)
            .Select(s => new
            {
                id = s.Id,
                name = s.FullName,
                no = s.StudentNo,
                section = s.CurrentSection != null
                    ? s.CurrentSection.Grade.Name + " - " + s.CurrentSection.Name : ""
            })
            .ToListAsync();

        return Json(items);
    }
}
