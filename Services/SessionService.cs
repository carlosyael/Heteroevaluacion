using HeteroevaluacionApp.Models;

namespace HeteroevaluacionApp.Services;

public class SessionService
{
    public Student? CurrentStudent { get; set; }
    public int? CurrentStudentClassId { get; set; }
    public bool IsTeacherAuthenticated { get; set; }
    public int? SelectedClassId { get; set; }
}
