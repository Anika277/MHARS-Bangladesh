using System.ComponentModel.DataAnnotations;

namespace MHARS.Web.Models;

public class Shelter
{
    public int Id { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string District { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string Address { get; set; } = string.Empty;

    [Range(1, 1000000)]
    public int Capacity { get; set; }

    [Phone]
    [StringLength(20)]
    public string? ContactNumber { get; set; }
}
