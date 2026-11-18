using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeteroevaluacionApp.Models;

public class Evaluation
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string EvaluatorId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string EvaluatedId { get; set; } = string.Empty;

    public double TotalScore { get; set; }

    [MaxLength(50)]
    public string Classification { get; set; } = string.Empty;

    public bool Approved { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(EvaluatorId))]
    public Student? Evaluator { get; set; }

    [ForeignKey(nameof(EvaluatedId))]
    public Student? Evaluated { get; set; }

    public ICollection<EvaluationDetail> Details { get; set; } = new List<EvaluationDetail>();
}
