using System.ComponentModel.DataAnnotations;

namespace HeteroevaluacionApp.Models;

public class Student
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Carrera { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string TemaTesis { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Evaluation> EvaluationsGiven { get; set; } = new List<Evaluation>();
    public ICollection<Evaluation> EvaluationsReceived { get; set; } = new List<Evaluation>();
}
