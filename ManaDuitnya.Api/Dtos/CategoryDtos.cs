using System.ComponentModel.DataAnnotations;

namespace ManaDuitnya.Api.Dtos;

public class CategoryDtos
{
    [Required] public DateOnly Month { get; set; }
    [Required] public long CategoryId { get; set; }
    [Required] public string Name { get; set; } = "";
    [Required] public short Type { get; set; }
    [Required] public string Color { get; set; } = "";
    public DateTime? ArchivedAt { get; set; }   
}