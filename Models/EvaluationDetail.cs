using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeteroevaluacionApp.Models;

public class EvaluationDetail
{
    [Key]
    public int Id { get; set; }

    public int EvaluationId { get; set; }

    [Required]
    [MaxLength(50)]
    public string CriterionKey { get; set; } = string.Empty;

    [Range(0, 4)]
    public int Score { get; set; }

    public double WeightedPoints { get; set; }

    [MaxLength(1000)]
    public string? Evidence { get; set; }

    [ForeignKey(nameof(EvaluationId))]
    public Evaluation? Evaluation { get; set; }
}
