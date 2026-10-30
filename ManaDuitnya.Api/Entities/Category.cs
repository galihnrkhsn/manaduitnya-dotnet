namespace ManaDuitnya.Api.Entities;

public enum CategoryType: short {Income = 1, Expense = 2, Transfer = 3}
public class Category
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Name { get; set; } = "";
    public CategoryType Type { get; set; }
    public string Color { get; set; } = "";
    public DateTime? ArchivedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}