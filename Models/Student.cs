using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeteroevaluacionApp.Models;

public class Student
{
    [Key]
    public int Id { get; set; }

    public int ClassRoomId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Matricula { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Carrera { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string TemaTesis { get; set; } = string.Empty;

    public int? WorkGroupId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ClassRoomId))]
    public ClassRoom? ClassRoom { get; set; }

    [ForeignKey(nameof(WorkGroupId))]
    public WorkGroup? WorkGroup { get; set; }

    public ICollection<Evaluation> EvaluationsGiven { get; set; } = new List<Evaluation>();
    public ICollection<Evaluation> EvaluationsReceived { get; set; } = new List<Evaluation>();
}
