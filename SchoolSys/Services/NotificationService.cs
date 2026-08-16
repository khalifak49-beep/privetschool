using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Hubs;
using SchoolSys.Models;

namespace SchoolSys.Services;

public interface INotificationService
{
    Task NotifyUserAsync(int userId, string title, string? body,
        NotificationType type = NotificationType.General,
        NotificationSeverity severity = NotificationSeverity.Info,
        string? link = null);

    Task NotifyUsersAsync(IEnumerable<int> userIds, string title, string? body,
        NotificationType type = NotificationType.General,
        NotificationSeverity severity = NotificationSeverity.Info,
        string? link = null);

    Task NotifyRoleAsync(string role, string title, string? body,
        NotificationType type = NotificationType.General,
        NotificationSeverity severity = NotificationSeverity.Info,
        string? link = null);

    /// <summary>إشعار أولياء أمور طالب داخل النظام + إدراج رسالة SMS/WhatsApp في الصادر.</summary>
    Task NotifyGuardiansOfStudentAsync(int studentId, string title, string body,
        NotificationType type = NotificationType.General,
        NotificationSeverity severity = NotificationSeverity.Info,
        string? link = null, bool alsoExternal = true);

    Task QueueExternalAsync(OutboxChannel channel, string recipient, string body,
        string? recipientName = null, string? relatedEntity = null, int? relatedId = null);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;

    public NotificationService(ApplicationDbContext db, IHubContext<NotificationHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public Task NotifyUserAsync(int userId, string title, string? body,
        NotificationType type = NotificationType.General,
        NotificationSeverity severity = NotificationSeverity.Info, string? link = null)
        => NotifyUsersAsync([userId], title, body, type, severity, link);

    public async Task NotifyUsersAsync(IEnumerable<int> userIds, string title, string? body,
        NotificationType type = NotificationType.General,
        NotificationSeverity severity = NotificationSeverity.Info, string? link = null)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var items = ids.Select(id => new Notification
        {
            UserId = id,
            Title = title,
            Body = body,
            NotificationType = type,
            Severity = severity,
            Link = link
        }).ToList();

        _db.Notifications.AddRange(items);
        await _db.SaveChangesAsync();

        foreach (var n in items)
            await _hub.Clients.Group($"user:{n.UserId}").SendAsync("ReceiveNotification", new
            {
                id = n.Id,
                title = n.Title,
                body = n.Body,
                severity = n.Severity.ToString().ToLowerInvariant(),
                link = n.Link,
                createdAt = n.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            });
    }

    public async Task NotifyRoleAsync(string role, string title, string? body,
        NotificationType type = NotificationType.General,
        NotificationSeverity severity = NotificationSeverity.Info, string? link = null)
    {
        var userIds = await (from ur in _db.UserRoles
                             join r in _db.Roles on ur.RoleId equals r.Id
                             where r.Name == role
                             select ur.UserId).ToListAsync();

        await NotifyUsersAsync(userIds, title, body, type, severity, link);
    }

    public async Task NotifyGuardiansOfStudentAsync(int studentId, string title, string body,
        NotificationType type = NotificationType.General,
        NotificationSeverity severity = NotificationSeverity.Info,
        string? link = null, bool alsoExternal = true)
    {
        var guardians = await _db.StudentGuardians
            .Where(sg => sg.StudentId == studentId)
            .Select(sg => new { sg.GuardianId, sg.Guardian.FullName, sg.Guardian.Phone })
            .ToListAsync();

        if (guardians.Count == 0) return;

        var guardianIds = guardians.Select(g => g.GuardianId).ToList();
        var userIds = await _db.Users
            .Where(u => u.GuardianId != null && guardianIds.Contains(u.GuardianId.Value))
            .Select(u => u.Id)
            .ToListAsync();

        if (userIds.Count > 0)
            await NotifyUsersAsync(userIds, title, body, type, severity, link);

        if (!alsoExternal) return;

        var settings = await _db.SchoolSettings.AsNoTracking().FirstOrDefaultAsync();
        var channel = settings?.EnableWhatsApp == true ? OutboxChannel.WhatsApp : OutboxChannel.Sms;

        foreach (var g in guardians.Where(g => !string.IsNullOrWhiteSpace(g.Phone)))
            await QueueExternalAsync(channel, g.Phone, $"{title}\n{body}", g.FullName, "Student", studentId);
    }

    public async Task QueueExternalAsync(OutboxChannel channel, string recipient, string body,
        string? recipientName = null, string? relatedEntity = null, int? relatedId = null)
    {
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Channel = channel,
            Recipient = recipient,
            RecipientName = recipientName,
            Body = body,
            Status = OutboxStatus.Queued,
            RelatedEntity = relatedEntity,
            RelatedEntityId = relatedId
        });
        await _db.SaveChangesAsync();
    }
}
