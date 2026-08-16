using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[Authorize]
public class CommunicationController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notify;
    private readonly ICurrentUserService _user;
    private readonly IFileStorageService _files;
    private readonly IAuditService _audit;

    public CommunicationController(ApplicationDbContext db, INotificationService notify,
        ICurrentUserService user, IFileStorageService files, IAuditService audit)
    {
        _db = db;
        _notify = notify;
        _user = user;
        _files = files;
        _audit = audit;
    }

    // ==================================================================
    // الإشعارات — واجهة الجرس
    // ==================================================================
    [HttpGet]
    public async Task<IActionResult> Latest()
    {
        var userId = _user.UserId;
        if (userId is null) return Json(new { unread = 0, items = Array.Empty<object>() });

        var items = await _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.Id)
            .Take(10)
            .Select(n => new
            {
                id = n.Id,
                title = n.Title,
                body = n.Body,
                severity = n.Severity.ToString().ToLower(),
                link = n.Link,
                isRead = n.IsRead,
                createdAt = n.CreatedAt
            })
            .ToListAsync();

        var unread = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        return Json(new
        {
            unread,
            items = items.Select(i => new
            {
                i.id, i.title, i.body, i.severity, i.link, i.isRead,
                createdAt = Helpers.ViewHelpers.Ago(i.createdAt)
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = _user.UserId;
        if (userId is null) return Json(new { ok = false });

        var unread = await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.Now;
        }
        await _db.SaveChangesAsync();

        return Json(new { ok = true, count = unread.Count });
    }

    public async Task<IActionResult> Notifications(bool onlyUnread = false, int page = 1)
    {
        var userId = _user.UserId;
        var vm = new NotificationsViewModel { OnlyUnread = onlyUnread, Page = page };

        var query = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (onlyUnread) query = query.Where(n => !n.IsRead);

        vm.Items = await PagedList<Notification>.CreateAsync(query.OrderByDescending(n => n.Id), page, 25);
        vm.UnreadCount = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = _user.UserId;
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (n is not null)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Notifications));
    }

    // ==================================================================
    // الإعلانات
    // ==================================================================
    [HasPermission(Permissions.AnnouncementsView)]
    public async Task<IActionResult> Announcements(AnnouncementAudience? audience, string? q, int page = 1)
    {
        var vm = new AnnouncementIndexViewModel { Audience = audience, Q = q, Page = page };
        var today = DateTime.Today;

        var query = _db.Announcements.AsNoTracking().AsQueryable();

        // من لا يملك صلاحية الإدارة يرى المنشور والصالح فقط
        if (!User.Can(Permissions.AnnouncementsManage))
            query = query.Where(a => a.IsPublished && a.PublishDate <= today &&
                                     (a.ExpiryDate == null || a.ExpiryDate >= today));

        if (audience.HasValue) query = query.Where(a => a.Audience == audience);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(a => a.Title.Contains(term) || a.Body.Contains(term));
        }

        vm.Items = await PagedList<Announcement>.CreateAsync(
            query.OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.PublishDate), page, 15);

        vm.ActiveCount = await _db.Announcements.CountAsync(a =>
            a.IsPublished && (a.ExpiryDate == null || a.ExpiryDate >= today));

        return View(vm);
    }

    [HasPermission(Permissions.AnnouncementsManage)]
    public IActionResult CreateAnnouncement() => View("AnnouncementForm", new AnnouncementFormViewModel());

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AnnouncementsManage)]
    public async Task<IActionResult> CreateAnnouncement(AnnouncementFormViewModel vm)
    {
        if (!ModelState.IsValid) return View("AnnouncementForm", vm);

        var a = new Announcement
        {
            Title = vm.Title.Trim(),
            Body = vm.Body,
            Audience = vm.Audience,
            PublishDate = vm.PublishDate,
            ExpiryDate = vm.ExpiryDate,
            IsPinned = vm.IsPinned,
            IsPublished = vm.IsPublished,
            CreatedByUserId = _user.UserId
        };

        try
        {
            a.AttachmentPath = await _files.SaveAsync(vm.Attachment, "announcements");
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(vm.Attachment), ex.Message);
            return View("AnnouncementForm", vm);
        }

        _db.Announcements.Add(a);
        await _db.SaveChangesAsync();

        if (vm.SendNotification && a.IsPublished)
            await BroadcastAnnouncementAsync(a);

        await _audit.LogAsync("نشر إعلان", nameof(Announcement), a.Id, a.Title);
        Success("تم نشر الإعلان.");
        return RedirectToAction(nameof(Announcements));
    }

    [HasPermission(Permissions.AnnouncementsManage)]
    public async Task<IActionResult> EditAnnouncement(int id)
    {
        var a = await _db.Announcements.FindAsync(id);
        if (a is null) return NotFound();

        return View("AnnouncementForm", new AnnouncementFormViewModel
        {
            Id = a.Id,
            Title = a.Title,
            Body = a.Body,
            Audience = a.Audience,
            PublishDate = a.PublishDate,
            ExpiryDate = a.ExpiryDate,
            AttachmentPath = a.AttachmentPath,
            IsPinned = a.IsPinned,
            IsPublished = a.IsPublished,
            SendNotification = false
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AnnouncementsManage)]
    public async Task<IActionResult> EditAnnouncement(AnnouncementFormViewModel vm)
    {
        if (!ModelState.IsValid) return View("AnnouncementForm", vm);

        var a = await _db.Announcements.FindAsync(vm.Id);
        if (a is null) return NotFound();

        a.Title = vm.Title.Trim();
        a.Body = vm.Body;
        a.Audience = vm.Audience;
        a.PublishDate = vm.PublishDate;
        a.ExpiryDate = vm.ExpiryDate;
        a.IsPinned = vm.IsPinned;
        a.IsPublished = vm.IsPublished;

        if (vm.Attachment is not null)
        {
            try
            {
                var path = await _files.SaveAsync(vm.Attachment, "announcements");
                if (path is not null)
                {
                    _files.Delete(a.AttachmentPath);
                    a.AttachmentPath = path;
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(vm.Attachment), ex.Message);
                return View("AnnouncementForm", vm);
            }
        }

        await _db.SaveChangesAsync();

        if (vm.SendNotification && a.IsPublished)
            await BroadcastAnnouncementAsync(a);

        Success("تم تحديث الإعلان.");
        return RedirectToAction(nameof(Announcements));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AnnouncementsManage)]
    public async Task<IActionResult> DeleteAnnouncement(int id)
    {
        var a = await _db.Announcements.FindAsync(id);
        if (a is not null)
        {
            _files.Delete(a.AttachmentPath);
            _db.Announcements.Remove(a);
            await _db.SaveChangesAsync();
            Success("تم حذف الإعلان.");
        }
        return RedirectToAction(nameof(Announcements));
    }

    private async Task BroadcastAnnouncementAsync(Announcement a)
    {
        var roles = a.Audience switch
        {
            AnnouncementAudience.Teachers => new[] { RoleNames.Teacher },
            AnnouncementAudience.Students => new[] { RoleNames.Student },
            AnnouncementAudience.Guardians => new[] { RoleNames.Guardian },
            AnnouncementAudience.Staff => new[]
            {
                RoleNames.Teacher, RoleNames.Accountant, RoleNames.Receptionist,
                RoleNames.AcademicAdmin, RoleNames.TransportManager, RoleNames.VicePrincipal
            },
            _ => Array.Empty<string>()
        };

        var body = a.Body.Length > 160 ? a.Body[..160] + "…" : a.Body;

        if (roles.Length == 0)
        {
            var allUserIds = await _db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync();
            await _notify.NotifyUsersAsync(allUserIds, a.Title, body,
                NotificationType.Announcement, NotificationSeverity.Info, "/Communication/Announcements");
        }
        else
        {
            foreach (var role in roles)
                await _notify.NotifyRoleAsync(role, a.Title, body,
                    NotificationType.Announcement, NotificationSeverity.Info, "/Communication/Announcements");
        }
    }

    // ==================================================================
    // الرسائل الداخلية
    // ==================================================================
    [HasPermission(Permissions.MessagesUse)]
    public async Task<IActionResult> Messages(string folder = "inbox", int page = 1)
    {
        var userId = _user.UserId;
        var vm = new MessagesViewModel { Folder = folder, Page = page };

        if (folder == "sent")
        {
            var sent = _db.Messages.AsNoTracking()
                .Where(m => m.SenderUserId == userId)
                .Select(m => new MessageRow
                {
                    Id = m.Id,
                    Subject = m.Subject,
                    Body = m.Body,
                    CounterpartName = string.Join("، ", m.Recipients.Take(3).Select(r => r.Recipient.FullName)),
                    SentAt = m.SentAt,
                    IsRead = true,
                    AttachmentPath = m.AttachmentPath,
                    RecipientCount = m.Recipients.Count
                });

            vm.Items = await PagedList<MessageRow>.CreateAsync(sent.OrderByDescending(m => m.Id), page, 20);
        }
        else
        {
            var inbox = _db.MessageRecipients.AsNoTracking()
                .Where(r => r.RecipientUserId == userId && !r.IsDeleted)
                .Select(r => new MessageRow
                {
                    Id = r.MessageId,
                    RecipientRowId = r.Id,
                    Subject = r.Message.Subject,
                    Body = r.Message.Body,
                    CounterpartName = r.Message.Sender.FullName,
                    SentAt = r.Message.SentAt,
                    IsRead = r.IsRead,
                    AttachmentPath = r.Message.AttachmentPath
                });

            vm.Items = await PagedList<MessageRow>.CreateAsync(inbox.OrderByDescending(m => m.Id), page, 20);
        }

        vm.UnreadCount = await _db.MessageRecipients
            .CountAsync(r => r.RecipientUserId == userId && !r.IsRead && !r.IsDeleted);

        return View(vm);
    }

    [HasPermission(Permissions.MessagesUse)]
    public async Task<IActionResult> Compose()
    {
        var vm = new ComposeMessageViewModel();
        await FillComposeAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.MessagesUse)]
    public async Task<IActionResult> Compose(ComposeMessageViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await FillComposeAsync(vm);
            return View(vm);
        }

        var senderId = _user.UserId;
        if (senderId is null) return Challenge();

        // تحديد المستلمين
        List<int> recipientIds;

        if (vm.TargetType == "section" && vm.SectionId.HasValue)
        {
            var studentIds = await _db.Students
                .Where(s => s.CurrentSectionId == vm.SectionId && s.Status == StudentStatus.Active)
                .Select(s => s.Id).ToListAsync();

            var guardianIds = await _db.StudentGuardians
                .Where(sg => studentIds.Contains(sg.StudentId))
                .Select(sg => sg.GuardianId).Distinct().ToListAsync();

            recipientIds = await _db.Users
                .Where(u => u.IsActive &&
                            ((u.StudentId != null && studentIds.Contains(u.StudentId.Value)) ||
                             (u.GuardianId != null && guardianIds.Contains(u.GuardianId.Value))))
                .Select(u => u.Id).ToListAsync();
        }
        else if (vm.TargetType == "users" && vm.UserIds.Count > 0)
        {
            recipientIds = vm.UserIds;
        }
        else if (!string.IsNullOrEmpty(vm.Role))
        {
            recipientIds = await (from ur in _db.UserRoles
                                  join r in _db.Roles on ur.RoleId equals r.Id
                                  join u in _db.Users on ur.UserId equals u.Id
                                  where r.Name == vm.Role && u.IsActive
                                  select ur.UserId).Distinct().ToListAsync();
        }
        else
        {
            Error("الرجاء تحديد المستلمين.");
            await FillComposeAsync(vm);
            return View(vm);
        }

        recipientIds = recipientIds.Where(id => id != senderId).Distinct().ToList();

        if (recipientIds.Count == 0)
        {
            Error("لم يتم العثور على مستلمين مطابقين.");
            await FillComposeAsync(vm);
            return View(vm);
        }

        var message = new Message
        {
            SenderUserId = senderId.Value,
            Subject = vm.Subject.Trim(),
            Body = vm.Body
        };

        try
        {
            message.AttachmentPath = await _files.SaveAsync(vm.Attachment, "messages");
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(vm.Attachment), ex.Message);
            await FillComposeAsync(vm);
            return View(vm);
        }

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

        _db.MessageRecipients.AddRange(recipientIds.Select(id => new MessageRecipient
        {
            MessageId = message.Id,
            RecipientUserId = id
        }));
        await _db.SaveChangesAsync();

        await _notify.NotifyUsersAsync(recipientIds, $"رسالة: {message.Subject}",
            message.Body.Length > 120 ? message.Body[..120] + "…" : message.Body,
            NotificationType.Message, NotificationSeverity.Info, "/Communication/Messages");

        // إرسال خارجي اختياري
        if (vm.AlsoExternal && User.Can(Permissions.NotificationsSend))
        {
            var settings = await GetSettingsAsync();
            var channel = settings.EnableWhatsApp ? OutboxChannel.WhatsApp : OutboxChannel.Sms;

            var phones = await _db.Users
                .Where(u => recipientIds.Contains(u.Id) && u.PhoneNumber != null)
                .Select(u => new { u.PhoneNumber, u.FullName })
                .ToListAsync();

            foreach (var p in phones)
                await _notify.QueueExternalAsync(channel, p.PhoneNumber!,
                    $"{message.Subject}\n{message.Body}", p.FullName, "Message", message.Id);
        }

        await _audit.LogAsync("إرسال رسالة داخلية", nameof(Message), message.Id,
            $"{recipientIds.Count} مستلم");

        Success($"تم إرسال الرسالة إلى {recipientIds.Count} مستخدم.");
        return RedirectToAction(nameof(Messages), new { folder = "sent" });
    }

    private async Task FillComposeAsync(ComposeMessageViewModel vm)
    {
        var year = await GetCurrentYearAsync();

        vm.Roles = RoleNames.Arabic
            .Select(kv => new SelectListItem(kv.Value, kv.Key, kv.Key == vm.Role))
            .ToList();

        vm.Sections = await _db.Sections.AsNoTracking()
            .Where(s => s.IsActive && (year == null || s.AcademicYearId == year.Id))
            .OrderBy(s => s.Grade.SeqNo).ThenBy(s => s.Name)
            .Select(s => new SelectListItem(s.Grade.Name + " - " + s.Name, s.Id.ToString(), s.Id == vm.SectionId))
            .ToListAsync();
    }

    [HasPermission(Permissions.MessagesUse)]
    public async Task<IActionResult> ReadMessage(int id)
    {
        var userId = _user.UserId;

        var message = await _db.Messages
            .Include(m => m.Sender)
            .Include(m => m.Recipients).ThenInclude(r => r.Recipient)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (message is null) return NotFound();

        var isRecipient = message.Recipients.Any(r => r.RecipientUserId == userId);
        if (!isRecipient && message.SenderUserId != userId) return Forbid();

        var row = message.Recipients.FirstOrDefault(r => r.RecipientUserId == userId);
        if (row is { IsRead: false })
        {
            row.IsRead = true;
            row.ReadAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }

        return View(message);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.MessagesUse)]
    public async Task<IActionResult> DeleteMessage(int recipientRowId)
    {
        var userId = _user.UserId;
        var row = await _db.MessageRecipients
            .FirstOrDefaultAsync(r => r.Id == recipientRowId && r.RecipientUserId == userId);

        if (row is not null)
        {
            row.IsDeleted = true;
            await _db.SaveChangesAsync();
            Success("تم حذف الرسالة.");
        }
        return RedirectToAction(nameof(Messages));
    }

    // ==================================================================
    // صادر SMS / واتساب
    // ==================================================================
    [HasPermission(Permissions.NotificationsSend)]
    public async Task<IActionResult> Outbox(OutboxStatus? status, OutboxChannel? channel, string? q, int page = 1)
    {
        var settings = await GetSettingsAsync();
        var vm = new OutboxViewModel
        {
            Status = status,
            Channel = channel,
            Q = q,
            Page = page,
            SmsEnabled = settings.EnableSms,
            WhatsAppEnabled = settings.EnableWhatsApp
        };

        var query = _db.OutboxMessages.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(m => m.Status == status);
        if (channel.HasValue) query = query.Where(m => m.Channel == channel);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(m => m.Recipient.Contains(term) ||
                                     (m.RecipientName != null && m.RecipientName.Contains(term)) ||
                                     m.Body.Contains(term));
        }

        vm.Items = await PagedList<OutboxMessage>.CreateAsync(query.OrderByDescending(m => m.Id), page, 30);

        vm.QueuedCount = await _db.OutboxMessages.CountAsync(m => m.Status == OutboxStatus.Queued);
        vm.SentCount = await _db.OutboxMessages.CountAsync(m => m.Status == OutboxStatus.Sent);
        vm.FailedCount = await _db.OutboxMessages.CountAsync(m => m.Status == OutboxStatus.Failed);

        return View(vm);
    }

    /// <summary>
    /// تعليم الرسائل كمُرسَلة. في بيئة الإنتاج يُستبدل هذا باستدعاء مزوّد
    /// خدمة الرسائل (Twilio / WhatsApp Business API) عبر خدمة خلفية.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.NotificationsSend)]
    public async Task<IActionResult> ProcessOutbox()
    {
        var settings = await GetSettingsAsync();

        if (!settings.EnableSms && !settings.EnableWhatsApp)
        {
            Warning("خدمات الرسائل الخارجية غير مفعّلة. فعّلها من إعدادات المدرسة أولاً.");
            return RedirectToAction(nameof(Outbox));
        }

        var queued = await _db.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Queued)
            .Take(200)
            .ToListAsync();

        foreach (var m in queued)
        {
            m.Status = OutboxStatus.Sent;
            m.SentAt = DateTime.Now;
            m.Attempts++;
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("معالجة صادر الرسائل", nameof(OutboxMessage), null, $"{queued.Count} رسالة");

        Success($"تمت معالجة {queued.Count} رسالة من قائمة الانتظار.");
        return RedirectToAction(nameof(Outbox));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.NotificationsSend)]
    public async Task<IActionResult> CancelOutbox(int id)
    {
        var m = await _db.OutboxMessages.FindAsync(id);
        if (m is not null && m.Status == OutboxStatus.Queued)
        {
            m.Status = OutboxStatus.Cancelled;
            await _db.SaveChangesAsync();
            Success("تم إلغاء الرسالة.");
        }
        return RedirectToAction(nameof(Outbox));
    }
}
