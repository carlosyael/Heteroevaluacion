using System.ComponentModel.DataAnnotations;

namespace HeteroevaluacionApp.Models;

public class AppSetting
{
    [Key]
    public int Id { get; set; } = 1;

    [Required]
    [MaxLength(100)]
    public string TeacherPin { get; set; } = "profesor2026";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
