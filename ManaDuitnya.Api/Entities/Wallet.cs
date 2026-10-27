namespace ManaDuitnya.Api.Entities;
public class Wallet
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Name { get; set; } = "";
    public long? InitialBalance { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}