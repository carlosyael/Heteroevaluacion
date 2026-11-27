using System.ComponentModel.DataAnnotations;

namespace HeteroevaluacionApp.Models;

public class ClassRoom
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string TeacherName { get; set; } = "Ricardo Cesar Reinoso Rodriguez";

    [MaxLength(250)]
    public string ProgramName { get; set; } = "Maestría en Enseñanza de la Matemática en el Nivel Secundario";

    [MaxLength(2500)]
    public string? Syllabus { get; set; }

    public double MinPassingScore { get; set; } = 70.0;

    public bool IsArchived { get; set; }

    public bool PublishResultsToStudents { get; set; }

    public DateTime? RoundEndsAtUtc { get; set; }

    public bool IsEvaluationOpen { get; set; }

    public int? ActiveGroupId { get; set; }

    [MaxLength(150)]
    public string? ActiveGroupName { get; set; }

    [MaxLength(2000)]
    public string? ActiveStudentIdsJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<WorkGroup> WorkGroups { get; set; } = new List<WorkGroup>();
    public ICollection<CriterionConfig> CriterionConfigs { get; set; } = new List<CriterionConfig>();
    public ICollection<Evaluation> Evaluations { get; set; } = new List<Evaluation>();
}
