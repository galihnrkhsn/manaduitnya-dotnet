using System.ComponentModel.DataAnnotations;
using ManaDuitnya.Api.Entities;

namespace ManaDuitnya.Api.Dtos;

public class WalletDtos
{
    [Required, StringLength(50)] public string Name { get; set; } = "";
    [Range(0, long.MaxValue)] public long InitialBalance { get; set; }
    public DateTime ArchivedAt { get; set; }
}

public class WalletResponse
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Name { get; set; } = "";
    public long InitialBalance { get; set; }
    public long Balance { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}