using System.ComponentModel.DataAnnotations;

namespace HeteroevaluacionApp.Models;

public class EvaluationState
{
    [Key]
    public int Id { get; set; } = 1;

    [MaxLength(50)]
    public string? ActiveStudentId { get; set; }

    public bool IsOpen { get; set; }

    public double MinPassingScore { get; set; } = 70.0;

    [MaxLength(100)]
    public string TeacherPin { get; set; } = "profesor2026";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
