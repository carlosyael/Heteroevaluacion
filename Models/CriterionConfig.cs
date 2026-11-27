using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeteroevaluacionApp.Models;

public class CriterionConfig
{
    [Key]
    public int Id { get; set; }

    public int ClassRoomId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Key { get; set; } = string.Empty;

    public int Order { get; set; }

    [Required]
    [MaxLength(60)]
    public string ShortName { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string FullTitle { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ChapterTag { get; set; } = string.Empty;

    public double Weight { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(ClassRoomId))]
    public ClassRoom? ClassRoom { get; set; }
}
