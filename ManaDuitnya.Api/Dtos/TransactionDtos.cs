using ManaDuitnya.Api.Entities;

namespace ManaDuitnya.Api.Dtos;

public class TransactionResponse
{
    public Guid Uuid { get; set; }
    public long UserId { get; set; }
    public long WalletId { get; set; }
    public long? ToWalletId { get; set; }
    public long? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? WalletName { get; set; }
    public string? ToWalletName { get; set; }
    public string? CategoryColor { get; set; }
    public TransactionType Type { get; set; }
    public long Amount { get; set; }
    public string? Note { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
