namespace MHARS.Web.Models;

public class SafetyGuideline
{
    public int Id { get; set; }

    public HazardType HazardType { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(500)]
    public string Text { get; set; } = string.Empty;

    public bool IsDo { get; set; }

    public int SortOrder { get; set; }
}
