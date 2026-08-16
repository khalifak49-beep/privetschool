using SchoolSys.Data;
using SchoolSys.Models;

namespace SchoolSys.Services;

public interface IAuditService
{
    Task LogAsync(string action, string? entityName = null, object? entityId = null, string? details = null);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;

    public AuditService(ApplicationDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task LogAsync(string action, string? entityName = null, object? entityId = null, string? details = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = _user.UserId,
            UserName = _user.FullName ?? _user.UserName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId?.ToString(),
            Details = details is { Length: > 2000 } ? details[..2000] : details,
            IpAddress = _user.IpAddress
        });
        await _db.SaveChangesAsync();
    }
}
