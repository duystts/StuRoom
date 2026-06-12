namespace StuRoom.Models;

public enum InvoiceStatus { Draft, Sent, Paid, Overdue, Cancelled }

public class Invoice
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    public int BillingYear { get; set; }
    public int BillingMonth { get; set; }

    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTime DueDate { get; set; }
    public string? Notes { get; set; }
    public bool DueReminderSent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InvoiceItem> Items { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}

public class InvoiceItem
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public int? FeeConfigId { get; set; }
    public FeeConfig? FeeConfig { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Chỉ số trước (cho PerUnit như điện, nước)</summary>
    public decimal? PreviousReading { get; set; }
    public decimal? CurrentReading { get; set; }
}
