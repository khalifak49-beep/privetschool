using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Security;

namespace SchoolSys.Services;

public interface ICurrentUserService
{
    int? UserId { get; }
    string? UserName { get; }
    string? FullName { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
    bool HasPermission(string permission);
    string? IpAddress { get; }

    /// <summary>معرّف سجل الموظف المرتبط بالحساب (للمعلمين والإداريين).</summary>
    Task<int?> GetEmployeeIdAsync();
    Task<int?> GetStudentIdAsync();
    Task<int?> GetGuardianIdAsync();
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;
    private readonly ApplicationDbContext _db;
    private (bool Loaded, int? Employee, int? Student, int? Guardian) _links;

    public CurrentUserService(IHttpContextAccessor http, ApplicationDbContext db)
    {
        _http = http;
        _db = db;
    }

    private ClaimsPrincipal? User => _http.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public int? UserId
    {
        get
        {
            var raw = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? UserName => User?.Identity?.Name;

    public string? FullName => User?.FindFirst("FullName")?.Value ?? UserName;

    public string? IpAddress => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool IsInRole(string role) => User?.IsInRole(role) == true;

    public bool HasPermission(string permission)
    {
        if (User is null) return false;
        if (User.IsInRole(RoleNames.SuperAdmin)) return true;
        return User.Claims.Any(c => c.Type == Permissions.ClaimType &&
                                    string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<int?> GetEmployeeIdAsync() => (await LoadAsync()).Employee;
    public async Task<int?> GetStudentIdAsync() => (await LoadAsync()).Student;
    public async Task<int?> GetGuardianIdAsync() => (await LoadAsync()).Guardian;

    private async Task<(bool Loaded, int? Employee, int? Student, int? Guardian)> LoadAsync()
    {
        if (_links.Loaded) return _links;

        var id = UserId;
        if (id is null)
        {
            _links = (true, null, null, null);
            return _links;
        }

        var row = await _db.Users
            .Where(u => u.Id == id)
            .Select(u => new { u.EmployeeId, u.StudentId, u.GuardianId })
            .FirstOrDefaultAsync();

        _links = (true, row?.EmployeeId, row?.StudentId, row?.GuardianId);
        return _links;
    }
}
