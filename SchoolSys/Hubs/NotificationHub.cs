using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SchoolSys.Hubs;

/// <summary>
/// قناة الإشعارات الفورية. كل مستخدم ينضم تلقائياً لمجموعة باسم معرّفه،
/// ويمكن البث لمجموعات الأدوار (role:Teacher ...).
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        if (Context.User is not null)
            foreach (var role in Context.User.Claims
                         .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                         .Select(c => c.Value))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{role}");

        await base.OnConnectedAsync();
    }
}
