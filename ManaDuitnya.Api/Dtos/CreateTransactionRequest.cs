using System.ComponentModel.DataAnnotations;
using ManaDuitnya.Api.Entities;

namespace ManaDuitnya.Api.Dtos;

public class CreateTransactionRequest {
    [Required] public long WalletId { get; set; }
    public long? ToWalletId { get; set; }
    public long? CategoryId { get; set; }
    [Required] public TransactionType Type { get; set; }
    [Range(1, long.MaxValue)] public long Amount { get; set; }
    public string? Note { get; set; }
    [Required] public DateTime TransactionDate { get; set; }
}