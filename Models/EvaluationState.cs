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

    [MaxLength(150)]
    public string TeacherName { get; set; } = "Ricardo Cesar Reinoso Rodriguez";

    [MaxLength(250)]
    public string ProgramName { get; set; } = "Maestría en Enseñanza de la Matemática en el Nivel Secundario";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
