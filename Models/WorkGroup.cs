using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeteroevaluacionApp.Models;

public class WorkGroup
{
    [Key]
    public int Id { get; set; }

    public int ClassRoomId { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Topic { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ClassRoomId))]
    public ClassRoom? ClassRoom { get; set; }

    public ICollection<Student> Members { get; set; } = new List<Student>();
}
