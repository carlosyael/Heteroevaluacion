using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeteroevaluacionApp.Models;

public class Evaluation
{
    [Key]
    public int Id { get; set; }

    public int ClassRoomId { get; set; }

    public int EvaluatorId { get; set; }

    public int EvaluatedId { get; set; }

    public int? WorkGroupId { get; set; }

    [MaxLength(64)]
    public string? GroupSubmissionKey { get; set; }

    public double TotalScore { get; set; }

    [MaxLength(50)]
    public string Classification { get; set; } = string.Empty;

    public bool Approved { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ClassRoomId))]
    public ClassRoom? ClassRoom { get; set; }

    [ForeignKey(nameof(EvaluatorId))]
    public Student? Evaluator { get; set; }

    [ForeignKey(nameof(EvaluatedId))]
    public Student? Evaluated { get; set; }

    [ForeignKey(nameof(WorkGroupId))]
    public WorkGroup? WorkGroup { get; set; }

    public ICollection<EvaluationDetail> Details { get; set; } = new List<EvaluationDetail>();
}
