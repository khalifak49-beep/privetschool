using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolSys.Models;
using System.ComponentModel.DataAnnotations;

namespace SchoolSys.ViewModels;

public class AnnouncementIndexViewModel
{
    public AnnouncementAudience? Audience { get; set; }
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    public PagedList<Announcement> Items { get; set; } = PagedList<Announcement>.Empty();
    public int ActiveCount { get; set; }
}

public class AnnouncementFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "الرجاء إدخال عنوان الإعلان")]
    [StringLength(200), Display(Name = "العنوان")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "الرجاء إدخال محتوى الإعلان")]
    [StringLength(4000), Display(Name = "المحتوى")]
    public string Body { get; set; } = string.Empty;

    [Display(Name = "الفئة المستهدفة")]
    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.All;

    [Display(Name = "تاريخ النشر"), DataType(DataType.Date)]
    public DateTime PublishDate { get; set; } = DateTime.Today;

    [Display(Name = "تاريخ الانتهاء"), DataType(DataType.Date)]
    public DateTime? ExpiryDate { get; set; }

    [Display(Name = "مرفق")]
    public IFormFile? Attachment { get; set; }
    public string? AttachmentPath { get; set; }

    [Display(Name = "تثبيت في الأعلى")]
    public bool IsPinned { get; set; }

    [Display(Name = "منشور")]
    public bool IsPublished { get; set; } = true;

    [Display(Name = "إرسال إشعار فوري للفئة المستهدفة")]
    public bool SendNotification { get; set; } = true;

    public bool IsEdit => Id > 0;
}

public class MessagesViewModel
{
    public string Folder { get; set; } = "inbox";   // inbox | sent
    public int Page { get; set; } = 1;
    public PagedList<MessageRow> Items { get; set; } = PagedList<MessageRow>.Empty();
    public int UnreadCount { get; set; }
}

public class MessageRow
{
    public int Id { get; set; }
    public int RecipientRowId { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string CounterpartName { get; set; } = "";
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
    public string? AttachmentPath { get; set; }
    public int RecipientCount { get; set; }
}

public class ComposeMessageViewModel
{
    [Required(ErrorMessage = "الرجاء إدخال الموضوع")]
    [StringLength(200), Display(Name = "الموضوع")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "الرجاء كتابة نص الرسالة")]
    [StringLength(4000), Display(Name = "نص الرسالة")]
    public string Body { get; set; } = string.Empty;

    [Display(Name = "إرسال إلى")]
    public string TargetType { get; set; } = "role";   // role | section | users

    [Display(Name = "الدور")]
    public string? Role { get; set; }

    [Display(Name = "الشعبة")]
    public int? SectionId { get; set; }

    [Display(Name = "مستخدمون محددون")]
    public List<int> UserIds { get; set; } = [];

    [Display(Name = "مرفق")]
    public IFormFile? Attachment { get; set; }

    [Display(Name = "إرسال رسالة نصية / واتساب أيضاً")]
    public bool AlsoExternal { get; set; }

    public List<SelectListItem> Roles { get; set; } = [];
    public List<SelectListItem> Sections { get; set; } = [];
}

public class NotificationsViewModel
{
    public bool OnlyUnread { get; set; }
    public int Page { get; set; } = 1;
    public PagedList<Notification> Items { get; set; } = PagedList<Notification>.Empty();
    public int UnreadCount { get; set; }
}

public class OutboxViewModel
{
    public OutboxStatus? Status { get; set; }
    public OutboxChannel? Channel { get; set; }
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    public PagedList<OutboxMessage> Items { get; set; } = PagedList<OutboxMessage>.Empty();
    public int QueuedCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public bool SmsEnabled { get; set; }
    public bool WhatsAppEnabled { get; set; }
}
