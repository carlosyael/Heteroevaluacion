using HeteroevaluacionApp.Models;

namespace HeteroevaluacionApp.Services;

public class SessionService
{
    public Student? CurrentStudent { get; set; }
    public bool IsTeacherAuthenticated { get; set; }
}
