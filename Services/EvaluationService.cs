using System.IO.Compression;
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
    private readonly IWebHostEnvironment _env;

    private static readonly string[] TemplateCriterionKeys =
    {
        "planteamiento",
        "justificacion",
        "literatura",
        "metodologia",
        "resultados",
        "conclusiones",
        "apa7",
        "redaccion",
        "formato"
    };

    public EvaluationService(IDbContextFactory<AppDbContext> dbFactory, IWebHostEnvironment env)
    {
        _dbFactory = dbFactory;
        _env = env;
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

    public async Task<List<CriterionConfig>> GetCriterionConfigsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var list = await db.CriterionConfigs.AsNoTracking().OrderBy(c => c.Order).ToListAsync();
        if (list.Count == 0)
        {
            list = RubricDefinition.Criteria.Select(c => new CriterionConfig
            {
                Key = c.Key,
                Order = c.Order,
                ShortName = c.ShortName,
                FullTitle = c.FullTitle,
                ChapterTag = c.ChapterTag,
                Weight = c.Weight,
                IsActive = true
            }).ToList();
            db.CriterionConfigs.AddRange(list);
            await db.SaveChangesAsync();
        }
        return list;
    }

    public async Task<(bool Success, string Message)> SaveCriterionConfigsAsync(List<CriterionConfig> configs)
    {
        var active = configs.Where(c => c.IsActive).ToList();
        if (active.Count == 0)
            return (false, "Debe haber al menos un criterio activo.");

        double totalWeight = Math.Round(active.Sum(c => c.Weight), 2);
        if (Math.Abs(totalWeight - 100.0) > 0.01)
            return (false, $"La suma de los pesos activos debe ser 100% (actualmente es {totalWeight:0.##}%).");

        await using var db = await _dbFactory.CreateDbContextAsync();
        foreach (var item in configs)
        {
            var existing = await db.CriterionConfigs.FindAsync(item.Key);
            if (existing != null)
            {
                existing.ShortName = item.ShortName.Trim();
                existing.FullTitle = item.FullTitle.Trim();
                existing.ChapterTag = item.ChapterTag.Trim();
                existing.Weight = Math.Round(item.Weight, 2);
                existing.IsActive = item.IsActive;
            }
        }
        await db.SaveChangesAsync();
        return (true, "Criterios y pesos guardados correctamente.");
    }

    public async Task ResetCriterionConfigsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await db.CriterionConfigs.ExecuteDeleteAsync();
        var defaults = RubricDefinition.Criteria.Select(c => new CriterionConfig
        {
            Key = c.Key,
            Order = c.Order,
            ShortName = c.ShortName,
            FullTitle = c.FullTitle,
            ChapterTag = c.ChapterTag,
            Weight = c.Weight,
            IsActive = true
        }).ToList();
        db.CriterionConfigs.AddRange(defaults);
        await db.SaveChangesAsync();
    }

    public async Task<bool> ValidateTeacherPinAsync(string pin)
    {
        var (state, _) = await GetEvaluationStateAsync();
        return pin == state.TeacherPin;
    }

    public async Task<(bool Success, string Message)> UpdateSettingsAsync(
        double minPassingScore,
        string? newPin,
        string? teacherName = null,
        string? programName = null)
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
        if (!string.IsNullOrWhiteSpace(teacherName))
        {
            state.TeacherName = teacherName.Trim();
        }
        if (programName != null)
        {
            state.ProgramName = programName.Trim();
        }
        state.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, "Configuración actualizada correctamente.");
    }

    public async Task DeleteEvaluationAsync(int evaluationId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await db.EvaluationDetails.Where(d => d.EvaluationId == evaluationId).ExecuteDeleteAsync();
        await db.Evaluations.Where(e => e.Id == evaluationId).ExecuteDeleteAsync();
    }

    public async Task DeleteEvaluationsForStudentAsync(string evaluatedId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var evalIds = await db.Evaluations
            .Where(e => e.EvaluatedId == evaluatedId)
            .Select(e => e.Id)
            .ToListAsync();

        if (evalIds.Count > 0)
        {
            await db.EvaluationDetails.Where(d => evalIds.Contains(d.EvaluationId)).ExecuteDeleteAsync();
            await db.Evaluations.Where(e => e.EvaluatedId == evaluatedId).ExecuteDeleteAsync();
        }
    }

    public async Task DeleteAllEvaluationsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await db.EvaluationDetails.ExecuteDeleteAsync();
        await db.Evaluations.ExecuteDeleteAsync();
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

        var activeCriteria = (await GetCriterionConfigsAsync()).Where(c => c.IsActive).ToList();
        foreach (var criterion in activeCriteria)
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

    public async Task<(byte[] Bytes, string FileName)?> ExportStudentRubricExcelAsync(string evaluatedId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var student = await db.Students.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == evaluatedId);
        if (student == null) return null;

        var evaluations = await db.Evaluations.AsNoTracking()
            .Include(e => e.Details)
            .Where(e => e.EvaluatedId == evaluatedId)
            .ToListAsync();
        if (evaluations.Count == 0) return null;

        var avgScores = new Dictionary<string, double>();
        foreach (var key in TemplateCriterionKeys)
        {
            var matching = evaluations
                .SelectMany(e => e.Details)
                .Where(d => d.CriterionKey == key)
                .ToList();
            avgScores[key] = matching.Count > 0 ? matching.Average(d => d.Score) : 0.0;
        }

        var (state, _) = await GetEvaluationStateAsync();
        var configs = await GetCriterionConfigsAsync();
        var latestDateLocal = evaluations.Max(e => e.CreatedAt).AddHours(-4);

        var bytes = BuildRubricFromTemplate(student, avgScores, latestDateLocal, state, configs);
        var fileName = $"Rubrica_{SanitizeFilePart(student.Id)}_{SanitizeFilePart(student.Nombre)}.xlsx";
        return (bytes, fileName);
    }

    public async Task<(byte[] Bytes, string FileName)?> ExportSingleEvaluationRubricExcelAsync(int evaluationId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var eval = await db.Evaluations.AsNoTracking()
            .Include(e => e.Evaluated)
            .Include(e => e.Details)
            .FirstOrDefaultAsync(e => e.Id == evaluationId);
        if (eval?.Evaluated == null) return null;

        var scores = new Dictionary<string, double>();
        foreach (var key in TemplateCriterionKeys)
        {
            var det = eval.Details.FirstOrDefault(d => d.CriterionKey == key);
            scores[key] = det != null ? det.Score : 0.0;
        }

        var (state, _) = await GetEvaluationStateAsync();
        var configs = await GetCriterionConfigsAsync();
        var evalDateLocal = eval.CreatedAt.AddHours(-4);

        var bytes = BuildRubricFromTemplate(eval.Evaluated, scores, evalDateLocal, state, configs);
        var fileName = $"Rubrica_{SanitizeFilePart(eval.Evaluated.Id)}_{SanitizeFilePart(eval.Evaluated.Nombre)}_Eval_{SanitizeFilePart(eval.EvaluatorId)}.xlsx";
        return (bytes, fileName);
    }

    public async Task<(byte[] Bytes, string FileName)?> ExportAllRubricsZipAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var students = await db.Students.AsNoTracking()
            .Include(s => s.EvaluationsReceived)
                .ThenInclude(e => e.Details)
            .Where(s => s.EvaluationsReceived.Any())
            .OrderBy(s => s.Nombre)
            .ToListAsync();

        if (students.Count == 0) return null;

        var (state, _) = await GetEvaluationStateAsync();
        var configs = await GetCriterionConfigsAsync();

        using var zipMs = new MemoryStream();
        using (var archive = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var student in students)
            {
                var avgScores = new Dictionary<string, double>();
                foreach (var key in TemplateCriterionKeys)
                {
                    var matching = student.EvaluationsReceived
                        .SelectMany(e => e.Details)
                        .Where(d => d.CriterionKey == key)
                        .ToList();
                    avgScores[key] = matching.Count > 0 ? matching.Average(d => d.Score) : 0.0;
                }

                var latestDateLocal = student.EvaluationsReceived.Max(e => e.CreatedAt).AddHours(-4);
                var xlsxBytes = BuildRubricFromTemplate(student, avgScores, latestDateLocal, state, configs);
                var entryName = $"Rubrica_{SanitizeFilePart(student.Id)}_{SanitizeFilePart(student.Nombre)}.xlsx";

                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                await entryStream.WriteAsync(xlsxBytes);
            }
        }

        var zipFileName = $"Rubricas_Estudiantes_{DateTime.UtcNow.AddHours(-4):yyyyMMdd}.zip";
        return (zipMs.ToArray(), zipFileName);
    }

    private string ResolveTemplatePath()
    {
        var contentPath = Path.Combine(_env.ContentRootPath, "Templates", "RUBRICA_TRABAJO_FINAL.xlsx");
        if (File.Exists(contentPath)) return contentPath;

        var basePath = Path.Combine(AppContext.BaseDirectory, "Templates", "RUBRICA_TRABAJO_FINAL.xlsx");
        if (File.Exists(basePath)) return basePath;

        throw new FileNotFoundException("No se encontró la plantilla oficial RUBRICA_TRABAJO_FINAL.xlsx en Templates/.");
    }

    private byte[] BuildRubricFromTemplate(
        Student student,
        Dictionary<string, double> criterionScores0To4,
        DateTime dateLocal,
        EvaluationState state,
        List<CriterionConfig> configs)
    {
        var templateBytes = File.ReadAllBytes(ResolveTemplatePath());
        using var templateMs = new MemoryStream(templateBytes);
        using var wb = new XLWorkbook(templateMs);
        var ws = wb.Worksheet("Rúbrica TFM");

        var titleText = string.IsNullOrWhiteSpace(state.ProgramName)
            ? "RÚBRICA PARA LA EVALUACIÓN DE LOS TRABAJOS FINALES DE POSGRADO"
            : $"RÚBRICA PARA LA EVALUACIÓN DE LOS TRABAJOS FINALES DE POSGRADO\n({state.ProgramName.Trim()})";
        ws.Cell("C1").Value = titleText;

        var configMap = configs.ToDictionary(c => c.Key, c => c);

        for (int i = 0; i < TemplateCriterionKeys.Length; i++)
        {
            string key = TemplateCriterionKeys[i];
            int rubricRow = 5 + i;
            int calcRow = 17 + i;

            configMap.TryGetValue(key, out var cfg);
            bool isActive = cfg?.IsActive ?? true;
            double defaultWeight = RubricDefinition.Criteria[i].Weight;
            double weightFraction = isActive
                ? Math.Round((cfg?.Weight ?? defaultWeight) / 100.0, 4)
                : 0.0;

            double score04 = isActive && criterionScores0To4.TryGetValue(key, out var s)
                ? Math.Round(s, 2)
                : 0.0;

            string scoreFormat = Math.Abs(score04 - Math.Round(score04)) < 0.005 ? "0" : "0.00";

            var gCell = ws.Cell(rubricRow, 7);
            gCell.Value = score04;
            gCell.Style.NumberFormat.Format = scoreFormat;

            var eCell = ws.Cell(calcRow, 5);
            eCell.Value = weightFraction;

            var bCell = ws.Cell(calcRow, 2);
            bCell.FormulaA1 = $"G{rubricRow}";
            bCell.Style.NumberFormat.Format = scoreFormat;

            ws.Cell(calcRow, 4).FormulaA1 = $"B{calcRow}/C{calcRow}";
            ws.Cell(calcRow, 6).FormulaA1 = $"E{calcRow}*100";
            ws.Cell(calcRow, 7).FormulaA1 = $"D{calcRow}*F{calcRow}";
        }

        ws.Cell("G28").Value = state.MinPassingScore;

        // Footer: Nombre (A33:B33)
        var nombreRange = ws.Range("A33:B33");
        nombreRange.Merge();
        var a33 = ws.Cell("A33");
        a33.Value = student.Nombre;
        a33.Style.Font.FontName = "Arial";
        a33.Style.Font.FontSize = 9;
        a33.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        a33.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        nombreRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        // Footer: Matrícula (C33)
        var c33 = ws.Cell("C33");
        c33.Value = student.Id;
        c33.Style.Font.FontName = "Arial";
        c33.Style.Font.FontSize = 9;
        c33.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        c33.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        c33.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        // Footer: Fecha (D33)
        var d33 = ws.Cell("D33");
        d33.Value = dateLocal.ToString("dd/MM/yyyy");
        d33.Style.Font.FontName = "Arial";
        d33.Style.Font.FontSize = 9;
        d33.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        d33.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        d33.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        // Footer: Profesor (B35:C35)
        var profRange = ws.Range("B35:C35");
        profRange.Merge();
        var b35 = ws.Cell("B35");
        b35.Value = string.IsNullOrWhiteSpace(state.TeacherName)
            ? "Ricardo Cesar Reinoso Rodriguez"
            : state.TeacherName.Trim();
        b35.Style.Font.FontName = "Arial";
        b35.Style.Font.FontSize = 9;
        b35.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        b35.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        profRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        // Footer: Nombre del proyecto o caso (A38:G38)
        var temaRange = ws.Range("A38:G38");
        temaRange.Merge();
        var a38 = ws.Cell("A38");
        a38.Value = student.TemaTesis;
        a38.Style.Font.FontName = "Arial";
        a38.Style.Font.FontSize = 9;
        a38.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        a38.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        a38.Style.Alignment.WrapText = true;
        temaRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        if (student.TemaTesis.Length > 190)
            ws.Row(38).Height = 44;
        else if (student.TemaTesis.Length > 95)
            ws.Row(38).Height = 30;

        wb.RecalculateAllFormulas();

        using var outMs = new MemoryStream();
        wb.SaveAs(outMs);
        return outMs.ToArray();
    }

    private static string SanitizeFilePart(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "SinDato";
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(input.Trim()
            .Replace(' ', '_')
            .Where(c => !invalid.Contains(c) && c != '/' && c != '\\')
            .ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "SinDato" : cleaned;
    }
}
