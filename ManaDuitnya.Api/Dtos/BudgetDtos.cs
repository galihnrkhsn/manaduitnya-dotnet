using System.ComponentModel.DataAnnotations;
using ManaDuitnya.Api.Entities;

namespace ManaDuitnya.Api.Dtos;

public class BudgetDtos
{
    [Required] public DateOnly Month { get; set; }
    [Required] public long CategoryId { get; set; }
    [Required] public long Amount { get; set; }
    
}

public class CopyBudgetRequest
{
    public DateOnly FromMonth { get; set; }
    public DateOnly ToMonth { get; set; }
}