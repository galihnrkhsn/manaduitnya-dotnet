namespace ManaDuitnya.Api.Entities;

public class Budget
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long CategoryId { get; set; }
    public DateOnly Month { get; set; }
    public long Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Category? Category { get; set; }
}