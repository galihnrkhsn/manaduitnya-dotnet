using System.ComponentModel.DataAnnotations;
using ManaDuitnya.Api.Entities;

namespace ManaDuitnya.Api.Dtos;

public class BudgetDtos
{
    [Required] public DateOnly Month { get; set; }
    [Required] public long CategoryId { get; set; }
    [Required] public long Amount { get; set; }
    
}