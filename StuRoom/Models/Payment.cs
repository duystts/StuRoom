namespace StuRoom.Models;

public enum PaymentMethod { Cash, BankTransfer, MoMo, VNPay }

public class Payment
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public PaymentMethod Method { get; set; }
    public string? TransactionRef { get; set; }
    public string? Notes { get; set; }

    public string RecordedById { get; set; } = string.Empty;
    public ApplicationUser RecordedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
