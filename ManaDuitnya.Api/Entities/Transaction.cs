namespace ManaDuitnya.Api.Entities;

public enum TransactionType: short {Income = 1, Expense = 2, Transfer = 3}

public class Transaction
{
    public Guid Uuid { get; set; }
    public long UserId { get; set; }
    public long WalletId { get; set; }
    public long? ToWalletId { get; set; }
    public long? CategoryId { get; set; }
    public TransactionType Type { get; set; }
    public long Amount { get; set; }
    public string? Note { get; set; }
    public DateTime TransactionDate { get; set; }
}