using System.ComponentModel.DataAnnotations;

namespace SchoolSys.Models;

/// <summary>رسالة داخلية</summary>
public class Message
{
    public int Id { get; set; }

    [Display(Name = "المرسل")]
    public int SenderUserId { get; set; }
    public ApplicationUser Sender { get; set; } = null!;

    [Required, StringLength(200), Display(Name = "الموضوع")]
    public string Subject { get; set; } = string.Empty;

    [Required, StringLength(4000), Display(Name = "نص الرسالة")]
    public string Body { get; set; } = string.Empty;

    [StringLength(400), Display(Name = "مرفق")]
    public string? AttachmentPath { get; set; }

    [Display(Name = "رداً على")]
    public int? ParentMessageId { get; set; }
    public Message? ParentMessage { get; set; }

    public DateTime SentAt { get; set; } = DateTime.Now;

    public ICollection<MessageRecipient> Recipients { get; set; } = new List<MessageRecipient>();
}

/// <summary>مستلم الرسالة</summary>
public class MessageRecipient
{
    public int Id { get; set; }

    public int MessageId { get; set; }
    public Message Message { get; set; } = null!;

    [Display(Name = "المستلم")]
    public int RecipientUserId { get; set; }
    public ApplicationUser Recipient { get; set; } = null!;

    [Display(Name = "مقروءة")]
    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    [Display(Name = "محذوفة")]
    public bool IsDeleted { get; set; }
}

/// <summary>إشعار داخل النظام</summary>
public class Notification
{
    public int Id { get; set; }

    [Display(Name = "المستخدم")]
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    [Required, StringLength(200), Display(Name = "العنوان")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000), Display(Name = "النص")]
    public string? Body { get; set; }

    [Display(Name = "النوع")]
    public NotificationType NotificationType { get; set; } = NotificationType.General;

    [Display(Name = "الأهمية")]
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    [StringLength(400), Display(Name = "الرابط")]
    public string? Link { get; set; }

    [Display(Name = "مقروء")]
    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>إعلان مدرسي</summary>
public class Announcement
{
    public int Id { get; set; }

    [Required, StringLength(200), Display(Name = "العنوان")]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000), Display(Name = "المحتوى")]
    public string Body { get; set; } = string.Empty;

    [Display(Name = "الفئة المستهدفة")]
    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.All;

    [Display(Name = "تاريخ النشر"), DataType(DataType.Date)]
    public DateTime PublishDate { get; set; } = DateTime.Today;

    [Display(Name = "تاريخ الانتهاء"), DataType(DataType.Date)]
    public DateTime? ExpiryDate { get; set; }

    [StringLength(400), Display(Name = "مرفق")]
    public string? AttachmentPath { get; set; }

    [Display(Name = "مثبت")]
    public bool IsPinned { get; set; }

    [Display(Name = "منشور")]
    public bool IsPublished { get; set; } = true;

    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>صندوق صادر الرسائل الخارجية (SMS / WhatsApp / Email)</summary>
public class OutboxMessage
{
    public int Id { get; set; }

    [Display(Name = "القناة")]
    public OutboxChannel Channel { get; set; } = OutboxChannel.Sms;

    [Required, StringLength(150), Display(Name = "المرسل إليه")]
    public string Recipient { get; set; } = string.Empty;

    [StringLength(150), Display(Name = "الاسم")]
    public string? RecipientName { get; set; }

    [Required, StringLength(2000), Display(Name = "النص")]
    public string Body { get; set; } = string.Empty;

    [Display(Name = "الحالة")]
    public OutboxStatus Status { get; set; } = OutboxStatus.Queued;

    [StringLength(1000), Display(Name = "رسالة الخطأ")]
    public string? Error { get; set; }

    [Display(Name = "عدد المحاولات")]
    public int Attempts { get; set; }

    [StringLength(60)]
    public string? RelatedEntity { get; set; }
    public int? RelatedEntityId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? SentAt { get; set; }
}
