namespace StuRoom.Models;

public enum NotificationType
{
    // Tenant nhận
    ViewingConfirmed,
    ViewingRescheduled,
    ViewingCancelled,
    InvoiceDue,
    ContractExpiring,
    ContractSigned,

    // Landlord nhận
    NewViewingRequest,
    NewBookingRequest,
    PaymentReceived,
    NewReview,

    // Tenant nhận (booking)
    BookingApproved,
    BookingRejected,
}

public class Notification
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }

    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
