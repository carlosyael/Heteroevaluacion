using ClosedXML.Excel;
using HeteroevaluacionApp.Data;
using HeteroevaluacionApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HeteroevaluacionApp.Services;

public record ClassmateStatus(
    Student Student,
    bool AlreadyEvaluated,
    double? ScoreGiven,
    string? ClassificationGiven
);

public record CriterionInput(string CriterionKey, int Score, string? Evidence);

public record StudentSummary(
    Student Student,
    int EvaluationsReceivedCount,
    int EvaluationsGivenCount,
    double AverageScore,
    string Classification,
    bool Approved
);

public class EvaluationService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public EvaluationService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<Student?> ValidateStudentAsync(string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId)) return null;
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Students.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId.Trim());
    }

    public async Task<List<ClassmateStatus>> GetClassmatesForEvaluatorAsync(string evaluatorId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var classmates = await db.Students.AsNoTracking()
            .Where(s => s.Id != evaluatorId)
            .OrderBy(s => s.Nombre)
            .ToListAsync();

        var given = await db.Evaluations.AsNoTracking()
            .Where(e => e.EvaluatorId == evaluatorId)
            .ToDictionaryAsync(e => e.EvaluatedId);

        return classmates.Select(c =>
        {
            given.TryGetValue(c.Id, out var eval);
            return new ClassmateStatus(
                c,
                eval != null,
                eval?.TotalScore,
                eval?.Classification
            );
        }).ToList();
    }

    public async Task<(EvaluationState State, Student? ActiveStudent)> GetEvaluationStateAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var state = await db.EvaluationStates.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1);
        if (state == null)
        {
            state = new EvaluationState { Id = 1, IsOpen = false, ActiveStudentId = null };
            db.EvaluationStates.Add(state);
            await db.SaveChangesAsync();
        }

        Student? activeStudent = null;
        if (!string.IsNullOrEmpty(state.ActiveStudentId))
        {
            activeStudent = await db.Students.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == state.ActiveStudentId);
        }

        return (state, activeStudent);
    }

    public async Task<(bool Success, string Message)> StartEvaluationAsync(string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
            return (false, "Selecciona un estudiante para iniciar la evaluación.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var exists = await db.Students.AnyAsync(s => s.Id == studentId);
        if (!exists)
            return (false, "El estudiante seleccionado no existe.");

        var state = await db.EvaluationStates.FirstOrDefaultAsync(s => s.Id == 1);
        if (state == null)
        {
            state = new EvaluationState { Id = 1 };
            db.EvaluationStates.Add(state);
        }

        state.ActiveStudentId = studentId;
        state.IsOpen = true;
        state.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return (true, "Proceso de evaluación iniciado.");
    }

    public async Task CloseEvaluationAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var state = await db.EvaluationStates.FirstOrDefaultAsync(s => s.Id == 1);
        if (state != null)
        {
            state.IsOpen = false;
            state.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public async Task<int> SeedSampleDataAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var samples = new List<Student>
        {
            new() { Id = "20210001", Nombre = "Ana García López", Carrera = "Ingeniería en Sistemas", TemaTesis = "Sistema de detección de anomalías con Machine Learning" },
            new() { Id = "20210002", Nombre = "Carlos Martínez Ruiz", Carrera = "Ingeniería Industrial", TemaTesis = "Optimización de cadena de suministro en manufactura" },
            new() { Id = "20210003", Nombre = "María Fernández Díaz", Carrera = "Ingeniería en Software", TemaTesis = "Arquitectura de microservicios para plataformas educativas" },
            new() { Id = "20210004", Nombre = "José Rodríguez Pérez", Carrera = "Ingeniería en Sistemas", TemaTesis = "Análisis predictivo de deserción estudiantil" },
            new() { Id = "20210005", Nombre = "Lucía Sánchez Gómez", Carrera = "Ingeniería Industrial", TemaTesis = "Implementación de Lean Manufacturing en PYMES" },
            new() { Id = "20210006", Nombre = "Diego Torres Castro", Carrera = "Ingeniería en Software", TemaTesis = "Seguridad en aplicaciones web con autenticación sin contraseña" }
        };

        int added = 0;
        foreach (var s in samples)
        {
            if (!await db.Students.AnyAsync(x => x.Id == s.Id))
            {
                db.Students.Add(s);
                added++;
            }
        }
        await db.SaveChangesAsync();
        return added;
    }

    public async Task<bool> ValidateTeacherPinAsync(string pin)
    {
        var (state, _) = await GetEvaluationStateAsync();
        return pin == state.TeacherPin;
    }

    public async Task<(bool Success, string Message)> UpdateSettingsAsync(double minPassingScore, string? newPin)
    {
        if (minPassingScore < 0 || minPassingScore > 100)
            return (false, "El mínimo aprobatorio debe estar entre 0 y 100.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var state = await db.EvaluationStates.FirstOrDefaultAsync(s => s.Id == 1);
        if (state == null)
        {
            state = new EvaluationState { Id = 1 };
            db.EvaluationStates.Add(state);
        }

        state.MinPassingScore = Math.Round(minPassingScore, 2);
        if (!string.IsNullOrWhiteSpace(newPin))
        {
            state.TeacherPin = newPin.Trim();
        }
        state.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, "Configuración actualizada correctamente.");
    }

    public async Task ResetAllDataAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await db.EvaluationDetails.ExecuteDeleteAsync();
        await db.Evaluations.ExecuteDeleteAsync();
        await db.Students.ExecuteDeleteAsync();

        var state = await db.EvaluationStates.FirstOrDefaultAsync(s => s.Id == 1);
        if (state != null)
        {
            state.ActiveStudentId = null;
            state.IsOpen = false;
            state.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public async Task<(bool Success, string Message)> SubmitEvaluationAsync(
        string evaluatorId,
        string evaluatedId,
        List<CriterionInput> inputs)
    {
        if (evaluatorId == evaluatedId)
            return (false, "No puedes evaluarte a ti mismo.");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var state = await db.EvaluationStates.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1);
        if (state == null || !state.IsOpen || state.ActiveStudentId != evaluatedId)
            return (false, "La evaluación para este estudiante no está activa en este momento.");

        var evaluatorExists = await db.Students.AnyAsync(s => s.Id == evaluatorId);
        var evaluatedExists = await db.Students.AnyAsync(s => s.Id == evaluatedId);
        if (!evaluatorExists || !evaluatedExists)
            return (false, "Estudiante no encontrado en el listado.");

        var alreadyExists = await db.Evaluations
            .AnyAsync(e => e.EvaluatorId == evaluatorId && e.EvaluatedId == evaluatedId);
        if (alreadyExists)
            return (false, "Ya has calificado a este estudiante anteriormente.");

        var details = new List<EvaluationDetail>();
        double total = 0;

        foreach (var criterion in RubricDefinition.Criteria)
        {
            var input = inputs.FirstOrDefault(i => i.CriterionKey == criterion.Key);
            int score = Math.Clamp(input?.Score ?? 0, 0, 4);
            double weighted = RubricDefinition.CalculateWeighted(criterion.Weight, score);
            total += weighted;

            details.Add(new EvaluationDetail
            {
                CriterionKey = criterion.Key,
                Score = score,
                WeightedPoints = weighted,
                Evidence = string.IsNullOrWhiteSpace(input?.Evidence) ? null : input.Evidence.Trim()
            });
        }

        total = Math.Round(total, 2);
        var evaluation = new Evaluation
        {
            EvaluatorId = evaluatorId,
            EvaluatedId = evaluatedId,
            TotalScore = total,
            Classification = RubricDefinition.Classify(total),
            Approved = total >= state.MinPassingScore,
            CreatedAt = DateTime.UtcNow,
            Details = details
        };

        db.Evaluations.Add(evaluation);
        await db.SaveChangesAsync();
        return (true, "Evaluación registrada correctamente.");
    }

    public async Task<List<Student>> GetAllStudentsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Students.AsNoTracking().OrderBy(s => s.Nombre).ToListAsync();
    }

    public async Task<(bool Success, string Message)> AddOrUpdateStudentAsync(Student student)
    {
        if (string.IsNullOrWhiteSpace(student.Id) || string.IsNullOrWhiteSpace(student.Nombre))
            return (false, "Id y Nombre son obligatorios.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.Students.FindAsync(student.Id.Trim());
        if (existing == null)
        {
            db.Students.Add(new Student
            {
                Id = student.Id.Trim(),
                Nombre = student.Nombre.Trim(),
                Carrera = student.Carrera.Trim(),
                TemaTesis = student.TemaTesis.Trim()
            });
        }
        else
        {
            existing.Nombre = student.Nombre.Trim();
            existing.Carrera = student.Carrera.Trim();
            existing.TemaTesis = student.TemaTesis.Trim();
        }
        await db.SaveChangesAsync();
        return (true, "Estudiante guardado.");
    }

    public async Task DeleteStudentAsync(string id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var s = await db.Students.FindAsync(id);
        if (s != null)
        {
            db.Students.Remove(s);
            await db.SaveChangesAsync();
        }
    }

    public async Task<List<StudentSummary>> GetTeacherSummariesAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var state = await db.EvaluationStates.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1);
        double minPassing = state?.MinPassingScore ?? 70.0;

        var students = await db.Students.AsNoTracking()
            .Include(s => s.EvaluationsReceived)
            .Include(s => s.EvaluationsGiven)
            .OrderBy(s => s.Nombre)
            .ToListAsync();

        return students.Select(s =>
        {
            int count = s.EvaluationsReceived.Count;
            double avg = count > 0 ? Math.Round(s.EvaluationsReceived.Average(e => e.TotalScore), 2) : 0;
            return new StudentSummary(
                s,
                count,
                s.EvaluationsGiven.Count,
                avg,
                count > 0 ? RubricDefinition.Classify(avg) : "Sin evaluar",
                count > 0 && avg >= minPassing
            );
        }).ToList();
    }

    public async Task<List<Evaluation>> GetEvaluationsForStudentAsync(string evaluatedId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Evaluations.AsNoTracking()
            .Include(e => e.Evaluator)
            .Include(e => e.Details)
            .Where(e => e.EvaluatedId == evaluatedId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<byte[]> ExportResultsExcelAsync()
    {
        var summaries = await GetTeacherSummariesAsync();
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Resultados");

        ws.Cell(1, 1).Value = "Id";
        ws.Cell(1, 2).Value = "Nombre";
        ws.Cell(1, 3).Value = "Carrera";
        ws.Cell(1, 4).Value = "Tema de Tesis";
        ws.Cell(1, 5).Value = "Evaluaciones Recibidas";
        ws.Cell(1, 6).Value = "Evaluaciones Realizadas";
        ws.Cell(1, 7).Value = "Promedio";
        ws.Cell(1, 8).Value = "Clasificación";
        ws.Cell(1, 9).Value = "Estado";

        int row = 2;
        foreach (var s in summaries)
        {
            ws.Cell(row, 1).Value = s.Student.Id;
            ws.Cell(row, 2).Value = s.Student.Nombre;
            ws.Cell(row, 3).Value = s.Student.Carrera;
            ws.Cell(row, 4).Value = s.Student.TemaTesis;
            ws.Cell(row, 5).Value = s.EvaluationsReceivedCount;
            ws.Cell(row, 6).Value = s.EvaluationsGivenCount;
            ws.Cell(row, 7).Value = s.AverageScore;
            ws.Cell(row, 8).Value = s.Classification;
            ws.Cell(row, 9).Value = s.EvaluationsReceivedCount == 0 ? "Pendiente" : (s.Approved ? "Aprobado" : "Reprobado");
            row++;
        }

        ws.Columns().AdjustToContents();

        // Sheet 2: Individual evaluations per student
        await using var db = await _dbFactory.CreateDbContextAsync();
        var allEvals = await db.Evaluations.AsNoTracking()
            .Include(e => e.Evaluated)
            .Include(e => e.Evaluator)
            .Include(e => e.Details)
            .OrderBy(e => e.EvaluatedId)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync();

        var wsDetail = wb.Worksheets.Add("Detalle Evaluaciones");
        wsDetail.Cell(1, 1).Value = "ID Evaluado";
        wsDetail.Cell(1, 2).Value = "Nombre Evaluado";
        wsDetail.Cell(1, 3).Value = "ID Evaluador";
        wsDetail.Cell(1, 4).Value = "Nombre Evaluador";
        wsDetail.Cell(1, 5).Value = "Puntaje Total";
        wsDetail.Cell(1, 6).Value = "Clasificación";
        wsDetail.Cell(1, 7).Value = "Estado";
        wsDetail.Cell(1, 8).Value = "Fecha (UTC)";

        int col = 9;
        foreach (var c in RubricDefinition.Criteria)
        {
            wsDetail.Cell(1, col++).Value = $"{c.ShortName} ({c.Weight}%)";
            wsDetail.Cell(1, col++).Value = $"Obs. {c.ShortName}";
        }

        int dRow = 2;
        foreach (var ev in allEvals)
        {
            wsDetail.Cell(dRow, 1).Value = ev.EvaluatedId;
            wsDetail.Cell(dRow, 2).Value = ev.Evaluated?.Nombre ?? "";
            wsDetail.Cell(dRow, 3).Value = ev.EvaluatorId;
            wsDetail.Cell(dRow, 4).Value = ev.Evaluator?.Nombre ?? "";
            wsDetail.Cell(dRow, 5).Value = ev.TotalScore;
            wsDetail.Cell(dRow, 6).Value = ev.Classification;
            wsDetail.Cell(dRow, 7).Value = ev.Approved ? "Aprobado" : "Reprobado";
            wsDetail.Cell(dRow, 8).Value = ev.CreatedAt.ToString("yyyy-MM-dd HH:mm");

            int cCol = 9;
            foreach (var c in RubricDefinition.Criteria)
            {
                var det = ev.Details.FirstOrDefault(x => x.CriterionKey == c.Key);
                wsDetail.Cell(dRow, cCol++).Value = det?.WeightedPoints ?? 0;
                wsDetail.Cell(dRow, cCol++).Value = det?.Evidence ?? "";
            }
            dRow++;
        }

        wsDetail.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
