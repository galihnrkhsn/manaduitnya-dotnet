using System.ComponentModel.DataAnnotations;
using ManaDuitnya.Api.Entities;

namespace ManaDuitnya.Api.Dtos;

public class WalletDtos
{
    [Required] public string Name { get; set; } = "";
    [Range(0, long.MaxValue)] public long InitialBalance { get; set; }
    public DateTime ArchivedAt { get; set; }
}