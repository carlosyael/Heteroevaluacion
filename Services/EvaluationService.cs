using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using HeteroevaluacionApp.Data;
using HeteroevaluacionApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HeteroevaluacionApp.Services;

public record CriterionInput(string CriterionKey, int Score, string? Evidence);

public record ActiveRoundInfo(
    bool IsOpen,
    int? ActiveGroupId,
    string? ActiveGroupName,
    List<Student> TargetStudents,
    string DisplayTopic,
    DateTime? RoundEndsAtUtc = null
);

public record StudentRoundStatus(
    ActiveRoundInfo Round,
    bool IsSelfOrInActiveTeam,
    bool AlreadyEvaluatedRound,
    double? ScoreGiven,
    string? ClassificationGiven
);

public record ActiveRoundProgress(
    int ReceivedUniqueEvaluators,
    int EligibleEvaluators,
    List<Student> PendingStudents,
    List<Student> CompletedStudents
);

public record StudentSummary(
    Student Student,
    string? GroupName,
    int EvaluationsReceivedCount,
    int EvaluationsGivenCount,
    double AverageScore,
    string Classification,
    bool Approved
);

public record GroupSummary(
    WorkGroup Group,
    List<Student> Members,
    int UniqueEvaluatorsCount,
    double AverageScore,
    string Classification,
    bool Approved
);

public record CriterionScoreDetail(
    string CriterionKey,
    int Score,
    double WeightedPoints,
    string? Evidence
);

public record GroupSubmissionDetail(
    string SubmissionKey,
    int EvaluatorId,
    string EvaluatorNombre,
    string EvaluatorMatricula,
    double TotalScore,
    string Classification,
    bool Approved,
    DateTime CreatedAt,
    bool IsOutlier,
    List<int> EvaluationIds,
    List<CriterionScoreDetail> Details
);

public record CriterionAnalyticsItem(
    CriterionConfig Config,
    double AverageScore0To4,
    double AverageWeightedPoints,
    double AchievementPercent,
    int EvaluationsCount
);

public record StudentAnonymousCriterionFeedback(
    string ShortName,
    string FullTitle,
    double Weight,
    double AverageWeightedPoints,
    double AchievementPercent,
    List<string> AnonymousComments
);

public record StudentFeedbackReport(
    int EvaluationsCount,
    double AverageScore,
    string Classification,
    bool Approved,
    double MinPassingScore,
    List<StudentAnonymousCriterionFeedback> Criteria
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

    public async Task<bool> ValidateTeacherPinAsync(string pin)
    {
        if (string.IsNullOrEmpty(pin)) return false;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Id == 1);
        var expected = setting?.TeacherPin
            ?? Environment.GetEnvironmentVariable("TEACHER_PIN")
            ?? "profesor2026";

        if (expected.StartsWith("pbkdf2:", StringComparison.Ordinal))
            return VerifyHashedPin(pin, expected);

        if (pin == expected)
        {
            if (setting != null)
            {
                setting.TeacherPin = HashPin(pin);
                setting.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return true;
        }
        return false;
    }

    public async Task<(bool Success, string Message)> UpdateTeacherPinAsync(string newPin)
    {
        if (string.IsNullOrWhiteSpace(newPin) || newPin.Trim().Length < 4)
            return (false, "El PIN debe tener al menos 4 caracteres.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Id == 1);
        if (setting == null)
        {
            setting = new AppSetting { Id = 1 };
            db.AppSettings.Add(setting);
        }
        setting.TeacherPin = HashPin(newPin.Trim());
        setting.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, "PIN de docente actualizado correctamente.");
    }

    private static string HashPin(string pin)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(pin), salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyHashedPin(string pin, string stored)
    {
        var parts = stored.Split(':');
        if (parts.Length != 3 || parts[0] != "pbkdf2") return false;
        try
        {
            byte[] salt = Convert.FromBase64String(parts[1]);
            byte[] expectedHash = Convert.FromBase64String(parts[2]);
            byte[] computed = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(pin), salt, 100_000, HashAlgorithmName.SHA256, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(computed, expectedHash);
        }
        catch
        {
            return false;
        }
    }

    public async Task<ActiveRoundInfo> GetActiveRoundAsync(int classRoomId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var cls = await db.ClassRooms.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classRoomId);
        if (cls == null || !cls.IsEvaluationOpen)
            return new ActiveRoundInfo(false, null, null, new(), string.Empty, null);

        if (cls.RoundEndsAtUtc.HasValue && cls.RoundEndsAtUtc.Value <= DateTime.UtcNow)
        {
            var tracked = await db.ClassRooms.FindAsync(classRoomId);
            if (tracked != null)
            {
                tracked.IsEvaluationOpen = false;
                tracked.RoundEndsAtUtc = null;
                tracked.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return new ActiveRoundInfo(false, null, null, new(), string.Empty, null);
        }

        var targetIds = ParseStudentIds(cls.ActiveStudentIdsJson);
        if (targetIds.Count == 0)
            return new ActiveRoundInfo(false, null, null, new(), string.Empty, null);

        var targets = await db.Students.AsNoTracking()
            .Include(s => s.WorkGroup)
            .Where(s => s.ClassRoomId == classRoomId && targetIds.Contains(s.Id))
            .OrderBy(s => s.Nombre)
            .ToListAsync();

        if (targets.Count == 0)
            return new ActiveRoundInfo(false, null, null, new(), string.Empty, null);

        string displayTopic = string.Empty;
        if (cls.ActiveGroupId.HasValue)
        {
            var grp = await db.WorkGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == cls.ActiveGroupId.Value);
            displayTopic = grp?.Topic ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(displayTopic))
            displayTopic = targets.First().TemaTesis;

        return new ActiveRoundInfo(cls.IsEvaluationOpen, cls.ActiveGroupId, cls.ActiveGroupName, targets, displayTopic, cls.RoundEndsAtUtc);
    }

    public async Task<(bool Success, string Message)> StartEvaluationRoundAsync(
        int classRoomId,
        List<int> targetStudentIds,
        int? groupId = null,
        string? groupName = null,
        int? durationMinutes = null)
    {
        var distinctIds = targetStudentIds.Distinct().ToList();
        if (distinctIds.Count == 0)
            return (false, "Selecciona al menos un estudiante para iniciar la evaluación.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var cls = await db.ClassRooms.FindAsync(classRoomId);
        if (cls == null)
            return (false, "Clase no encontrada.");

        var validIds = await db.Students
            .Where(s => s.ClassRoomId == classRoomId && distinctIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync();

        if (validIds.Count == 0)
            return (false, "Ninguno de los estudiantes seleccionados pertenece a esta clase.");

        cls.ActiveStudentIdsJson = JsonSerializer.Serialize(validIds);
        cls.ActiveGroupId = groupId;
        cls.ActiveGroupName = string.IsNullOrWhiteSpace(groupName) ? null : groupName.Trim();
        cls.IsEvaluationOpen = true;
        cls.RoundEndsAtUtc = durationMinutes.HasValue && durationMinutes.Value > 0
            ? DateTime.UtcNow.AddMinutes(durationMinutes.Value)
            : null;
        cls.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        string label = !string.IsNullOrWhiteSpace(cls.ActiveGroupName)
            ? $"para '{cls.ActiveGroupName}' ({validIds.Count} integrante(s))"
            : validIds.Count > 1
                ? $"para selección grupal ({validIds.Count} integrantes)"
                : "para el estudiante seleccionado";
        return (true, $"Evaluación iniciada {label}.");
    }

    public async Task CloseEvaluationRoundAsync(int classRoomId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var cls = await db.ClassRooms.FindAsync(classRoomId);
        if (cls != null)
        {
            cls.IsEvaluationOpen = false;
            cls.RoundEndsAtUtc = null;
            cls.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public async Task<Student?> ValidateStudentAsync(int classRoomId, string matricula)
    {
        if (string.IsNullOrWhiteSpace(matricula)) return null;
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Students.AsNoTracking()
            .Include(s => s.WorkGroup)
            .Include(s => s.ClassRoom)
            .FirstOrDefaultAsync(s => s.ClassRoomId == classRoomId && s.Matricula == matricula.Trim());
    }

    public async Task<StudentRoundStatus> GetStudentRoundStatusAsync(int classRoomId, int evaluatorStudentId)
    {
        var round = await GetActiveRoundAsync(classRoomId);
        if (!round.IsOpen || round.TargetStudents.Count == 0)
            return new StudentRoundStatus(round, false, false, null, null);

        var targetIds = round.TargetStudents.Select(t => t.Id).ToList();
        if (targetIds.Contains(evaluatorStudentId))
            return new StudentRoundStatus(round, true, false, null, null);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.Evaluations.AsNoTracking()
            .Where(e => e.ClassRoomId == classRoomId
                     && e.EvaluatorId == evaluatorStudentId
                     && targetIds.Contains(e.EvaluatedId))
            .ToListAsync();

        bool allTargetsEvaluated = existing.Count >= targetIds.Count;
        double? score = existing.Count > 0 ? Math.Round(existing.Average(e => e.TotalScore), 2) : null;
        string? classification = existing.FirstOrDefault()?.Classification;

        return new StudentRoundStatus(round, false, allTargetsEvaluated, score, classification);
    }

    public async Task<(bool Success, string Message)> SubmitRoundEvaluationAsync(
        int classRoomId,
        int evaluatorStudentId,
        List<CriterionInput> inputs)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var cls = await db.ClassRooms.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classRoomId);
        if (cls == null || !cls.IsEvaluationOpen)
            return (false, "La evaluación para esta clase no está activa en este momento.");

        if (cls.RoundEndsAtUtc.HasValue && cls.RoundEndsAtUtc.Value <= DateTime.UtcNow)
        {
            var tracked = await db.ClassRooms.FindAsync(classRoomId);
            if (tracked != null)
            {
                tracked.IsEvaluationOpen = false;
                tracked.RoundEndsAtUtc = null;
                tracked.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return (false, "El tiempo límite de esta ronda de evaluación ha finalizado.");
        }

        var targetIds = ParseStudentIds(cls.ActiveStudentIdsJson);
        if (targetIds.Count == 0)
            return (false, "No hay estudiantes activos en la ronda actual.");

        if (targetIds.Contains(evaluatorStudentId))
            return (false, "No puedes evaluarte a ti mismo ni a tu propio equipo.");

        var evaluatorExists = await db.Students.AnyAsync(s => s.Id == evaluatorStudentId && s.ClassRoomId == classRoomId);
        if (!evaluatorExists)
            return (false, "Estudiante no encontrado en esta clase.");

        var alreadyRatedTargetIds = await db.Evaluations
            .Where(e => e.ClassRoomId == classRoomId
                     && e.EvaluatorId == evaluatorStudentId
                     && targetIds.Contains(e.EvaluatedId))
            .Select(e => e.EvaluatedId)
            .ToListAsync();

        var pendingTargetIds = targetIds.Except(alreadyRatedTargetIds).ToList();
        if (pendingTargetIds.Count == 0)
            return (false, "Ya has calificado esta exposición anteriormente.");

        var activeCriteria = (await GetCriterionConfigsAsync(classRoomId)).Where(c => c.IsActive).ToList();
        double total = 0;
        var baseDetails = new List<(string Key, int Score, double Weighted, string? Evidence)>();

        foreach (var criterion in activeCriteria)
        {
            var input = inputs.FirstOrDefault(i => i.CriterionKey == criterion.Key);
            int score = Math.Clamp(input?.Score ?? 0, 0, 4);
            double weighted = RubricDefinition.CalculateWeighted(criterion.Weight, score);
            total += weighted;

            baseDetails.Add((
                criterion.Key,
                score,
                weighted,
                string.IsNullOrWhiteSpace(input?.Evidence) ? null : input.Evidence.Trim()
            ));
        }

        total = Math.Round(total, 2);
        string classification = RubricDefinition.Classify(total);
        bool approved = total >= cls.MinPassingScore;
        string submissionKey = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;

        foreach (var targetId in pendingTargetIds)
        {
            var eval = new Evaluation
            {
                ClassRoomId = classRoomId,
                EvaluatorId = evaluatorStudentId,
                EvaluatedId = targetId,
                WorkGroupId = cls.ActiveGroupId,
                GroupSubmissionKey = submissionKey,
                TotalScore = total,
                Classification = classification,
                Approved = approved,
                CreatedAt = now,
                Details = baseDetails.Select(d => new EvaluationDetail
                {
                    CriterionKey = d.Key,
                    Score = d.Score,
                    WeightedPoints = d.Weighted,
                    Evidence = d.Evidence
                }).ToList()
            };
            db.Evaluations.Add(eval);
        }

        await db.SaveChangesAsync();
        return (true, "Evaluación registrada correctamente.");
    }

    public async Task<ActiveRoundProgress> GetActiveRoundProgressAsync(int classRoomId)
    {
        var round = await GetActiveRoundAsync(classRoomId);
        if (!round.IsOpen || round.TargetStudents.Count == 0)
            return new ActiveRoundProgress(0, 0, new(), new());

        var targetIds = round.TargetStudents.Select(t => t.Id).ToHashSet();

        await using var db = await _dbFactory.CreateDbContextAsync();
        var eligible = await db.Students.AsNoTracking()
            .Where(s => s.ClassRoomId == classRoomId && !targetIds.Contains(s.Id))
            .OrderBy(s => s.Nombre)
            .ToListAsync();

        var completedIds = await db.Evaluations.AsNoTracking()
            .Where(e => e.ClassRoomId == classRoomId && targetIds.Contains(e.EvaluatedId))
            .Select(e => e.EvaluatorId)
            .Distinct()
            .ToListAsync();

        var completedSet = completedIds.ToHashSet();
        var pending = eligible.Where(s => !completedSet.Contains(s.Id)).ToList();
        var completed = eligible.Where(s => completedSet.Contains(s.Id)).ToList();

        return new ActiveRoundProgress(completed.Count, eligible.Count, pending, completed);
    }

    public async Task<List<Student>> GetAllStudentsAsync(int classRoomId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Students.AsNoTracking()
            .Include(s => s.WorkGroup)
            .Where(s => s.ClassRoomId == classRoomId)
            .OrderBy(s => s.Nombre)
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> AddOrUpdateStudentAsync(int classRoomId, Student student, bool isEditing = false)
    {
        if (string.IsNullOrWhiteSpace(student.Matricula) || string.IsNullOrWhiteSpace(student.Nombre))
            return (false, "Matrícula y Nombre son obligatorios.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var mat = student.Matricula.Trim();

        Student? existing = isEditing && student.Id > 0
            ? await db.Students.FirstOrDefaultAsync(s => s.Id == student.Id && s.ClassRoomId == classRoomId)
            : await db.Students.FirstOrDefaultAsync(s => s.ClassRoomId == classRoomId && s.Matricula == mat);

        if (existing == null)
        {
            db.Students.Add(new Student
            {
                ClassRoomId = classRoomId,
                Matricula = mat,
                Nombre = student.Nombre.Trim(),
                Carrera = string.IsNullOrWhiteSpace(student.Carrera) ? "Sin especificar" : student.Carrera.Trim(),
                TemaTesis = string.IsNullOrWhiteSpace(student.TemaTesis) ? "Sin especificar" : student.TemaTesis.Trim(),
                WorkGroupId = student.WorkGroupId
            });
        }
        else
        {
            existing.Nombre = student.Nombre.Trim();
            existing.Carrera = string.IsNullOrWhiteSpace(student.Carrera) ? existing.Carrera : student.Carrera.Trim();
            existing.TemaTesis = string.IsNullOrWhiteSpace(student.TemaTesis) ? existing.TemaTesis : student.TemaTesis.Trim();
            existing.WorkGroupId = student.WorkGroupId;
        }

        await db.SaveChangesAsync();
        return (true, isEditing ? "Estudiante actualizado correctamente." : "Estudiante guardado correctamente.");
    }

    public async Task DeleteStudentAsync(int studentId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var evalIds = await db.Evaluations
            .Where(e => e.EvaluatorId == studentId || e.EvaluatedId == studentId)
            .Select(e => e.Id)
            .ToListAsync();

        if (evalIds.Count > 0)
        {
            await db.EvaluationDetails.Where(d => evalIds.Contains(d.EvaluationId)).ExecuteDeleteAsync();
            await db.Evaluations.Where(e => evalIds.Contains(e.Id)).ExecuteDeleteAsync();
        }

        var s = await db.Students.FindAsync(studentId);
        if (s != null)
        {
            db.Students.Remove(s);
            await db.SaveChangesAsync();
        }
    }

    public async Task<List<CriterionConfig>> GetCriterionConfigsAsync(int classRoomId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var list = await db.CriterionConfigs.AsNoTracking()
            .Where(c => c.ClassRoomId == classRoomId)
            .OrderBy(c => c.Order)
            .ToListAsync();

        if (list.Count == 0)
        {
            list = RubricDefinition.Criteria.Select(c => new CriterionConfig
            {
                ClassRoomId = classRoomId,
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

    public async Task<(bool Success, string Message)> SaveCriterionConfigsAsync(int classRoomId, List<CriterionConfig> configs)
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
            var existing = await db.CriterionConfigs
                .FirstOrDefaultAsync(c => c.ClassRoomId == classRoomId && c.Key == item.Key);
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

    public async Task ResetCriterionConfigsAsync(int classRoomId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await db.CriterionConfigs.Where(c => c.ClassRoomId == classRoomId).ExecuteDeleteAsync();
        var defaults = RubricDefinition.Criteria.Select(c => new CriterionConfig
        {
            ClassRoomId = classRoomId,
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

    public async Task<List<StudentSummary>> GetStudentSummariesAsync(int classRoomId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var cls = await db.ClassRooms.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classRoomId);
        double minPassing = cls?.MinPassingScore ?? 70.0;

        var students = await db.Students.AsNoTracking()
            .Include(s => s.WorkGroup)
            .Include(s => s.EvaluationsReceived)
            .Include(s => s.EvaluationsGiven)
            .Where(s => s.ClassRoomId == classRoomId)
            .OrderBy(s => s.Nombre)
            .ToListAsync();

        return students.Select(s =>
        {
            int count = s.EvaluationsReceived.Count;
            double avg = count > 0 ? Math.Round(s.EvaluationsReceived.Average(e => e.TotalScore), 2) : 0;
            return new StudentSummary(
                s,
                s.WorkGroup?.Name,
                count,
                s.EvaluationsGiven.Count,
                avg,
                count > 0 ? RubricDefinition.Classify(avg) : "Sin evaluar",
                count > 0 && avg >= minPassing
            );
        }).ToList();
    }

    public async Task<List<GroupSummary>> GetGroupSummariesAsync(int classRoomId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var cls = await db.ClassRooms.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classRoomId);
        double minPassing = cls?.MinPassingScore ?? 70.0;

        var groups = await db.WorkGroups.AsNoTracking()
            .Include(g => g.Members.OrderBy(m => m.Nombre))
                .ThenInclude(m => m.EvaluationsReceived)
            .Where(g => g.ClassRoomId == classRoomId)
            .OrderBy(g => g.Name)
            .ToListAsync();

        return groups.Select(g =>
        {
            var members = g.Members.ToList();
            var allEvals = members.SelectMany(m => m.EvaluationsReceived).ToList();

            // Deduplicate by unique EvaluatorId so a group evaluated by N classmates reports N unique evaluations
            var uniqueByEvaluator = allEvals
                .GroupBy(e => e.EvaluatorId)
                .Select(grp => grp.Average(x => x.TotalScore))
                .ToList();

            int uniqueCount = uniqueByEvaluator.Count;
            double avg = uniqueCount > 0 ? Math.Round(uniqueByEvaluator.Average(), 2) : 0;

            return new GroupSummary(
                g,
                members,
                uniqueCount,
                avg,
                uniqueCount > 0 ? RubricDefinition.Classify(avg) : "Sin evaluar",
                uniqueCount > 0 && avg >= minPassing
            );
        }).ToList();
    }

    public async Task<List<Evaluation>> GetEvaluationsForStudentAsync(int evaluatedStudentId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Evaluations.AsNoTracking()
            .Include(e => e.Evaluator)
            .Include(e => e.Details)
            .Where(e => e.EvaluatedId == evaluatedStudentId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public static bool IsOutlierScore(double score, double average, int totalCount) =>
        totalCount >= 3 && Math.Abs(score - average) >= 20.0;

    public async Task<List<GroupSubmissionDetail>> GetGroupSubmissionDetailsAsync(int groupId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var group = await db.WorkGroups.AsNoTracking()
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null || group.Members.Count == 0) return new();

        var cls = await db.ClassRooms.AsNoTracking().FirstOrDefaultAsync(c => c.Id == group.ClassRoomId);
        double minPassing = cls?.MinPassingScore ?? 70.0;

        var memberIds = group.Members.Select(m => m.Id).ToList();
        var rawEvals = await db.Evaluations.AsNoTracking()
            .Include(e => e.Evaluator)
            .Include(e => e.Details)
            .Where(e => e.ClassRoomId == group.ClassRoomId && memberIds.Contains(e.EvaluatedId))
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        if (rawEvals.Count == 0) return new();

        var groupedByEvaluator = rawEvals.GroupBy(e => e.EvaluatorId).ToList();
        var tempItems = new List<GroupSubmissionDetail>();

        foreach (var g in groupedByEvaluator)
        {
            var latest = g.OrderByDescending(e => e.CreatedAt).First();
            double totalScore = Math.Round(g.Average(e => e.TotalScore), 2);
            var critDetails = g.SelectMany(e => e.Details)
                .GroupBy(d => d.CriterionKey)
                .Select(cg => new CriterionScoreDetail(
                    cg.Key,
                    (int)Math.Round(cg.Average(x => x.Score)),
                    Math.Round(cg.Average(x => x.WeightedPoints), 2),
                    cg.Select(x => x.Evidence).FirstOrDefault(ev => !string.IsNullOrWhiteSpace(ev))
                ))
                .ToList();

            tempItems.Add(new GroupSubmissionDetail(
                latest.GroupSubmissionKey ?? latest.Id.ToString(),
                g.Key,
                latest.Evaluator?.Nombre ?? "Desconocido",
                latest.Evaluator?.Matricula ?? "-",
                totalScore,
                RubricDefinition.Classify(totalScore),
                totalScore >= minPassing,
                latest.CreatedAt,
                false,
                g.Select(e => e.Id).ToList(),
                critDetails
            ));
        }

        double overallAvg = tempItems.Average(x => x.TotalScore);
        return tempItems
            .Select(item => item with { IsOutlier = IsOutlierScore(item.TotalScore, overallAvg, tempItems.Count) })
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }

    public async Task DeleteEvaluationBatchAsync(List<int> evaluationIds)
    {
        if (evaluationIds.Count == 0) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        await db.EvaluationDetails.Where(d => evaluationIds.Contains(d.EvaluationId)).ExecuteDeleteAsync();
        await db.Evaluations.Where(e => evaluationIds.Contains(e.Id)).ExecuteDeleteAsync();
    }

    public async Task<List<CriterionAnalyticsItem>> GetClassCriterionAnalyticsAsync(int classRoomId)
    {
        var configs = (await GetCriterionConfigsAsync(classRoomId)).Where(c => c.IsActive).ToList();
        if (configs.Count == 0) return new();

        await using var db = await _dbFactory.CreateDbContextAsync();
        var rawEvals = await db.Evaluations.AsNoTracking()
            .Include(e => e.Details)
            .Where(e => e.ClassRoomId == classRoomId)
            .ToListAsync();

        // Deduplicate Modelo 1 group submissions so a team size N counts once in class analytics
        var uniqueSubmissions = rawEvals
            .GroupBy(e => (e.EvaluatorId, Key: e.GroupSubmissionKey ?? e.Id.ToString()))
            .Select(g => g.First())
            .ToList();

        return configs.Select(cfg =>
        {
            var matching = uniqueSubmissions
                .SelectMany(e => e.Details)
                .Where(d => d.CriterionKey == cfg.Key)
                .ToList();

            if (matching.Count == 0)
                return new CriterionAnalyticsItem(cfg, 0, 0, 0, 0);

            double avg04 = Math.Round(matching.Average(d => (double)d.Score), 2);
            double avgWeighted = Math.Round(matching.Average(d => d.WeightedPoints), 2);
            double pct = Math.Clamp(Math.Round((avg04 / 4.0) * 100.0, 1), 0, 100);
            return new CriterionAnalyticsItem(cfg, avg04, avgWeighted, pct, matching.Count);
        }).ToList();
    }

    public async Task<StudentFeedbackReport?> GetStudentAnonymousFeedbackAsync(int classRoomId, int studentId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var cls = await db.ClassRooms.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classRoomId);
        if (cls == null || !cls.PublishResultsToStudents)
            return null;

        var evals = await db.Evaluations.AsNoTracking()
            .Include(e => e.Details)
            .Where(e => e.ClassRoomId == classRoomId && e.EvaluatedId == studentId)
            .ToListAsync();

        var configs = (await GetCriterionConfigsAsync(classRoomId)).Where(c => c.IsActive).ToList();
        if (evals.Count == 0)
            return new StudentFeedbackReport(0, 0, "Sin evaluar", false, cls.MinPassingScore, new());

        double avg = Math.Round(evals.Average(e => e.TotalScore), 2);
        var criteriaFeedback = configs.Select(cfg =>
        {
            var details = evals.SelectMany(e => e.Details).Where(d => d.CriterionKey == cfg.Key).ToList();
            double avgW = details.Count > 0 ? Math.Round(details.Average(d => d.WeightedPoints), 2) : 0;
            double avg04 = details.Count > 0 ? details.Average(d => (double)d.Score) : 0;
            double pct = Math.Clamp(Math.Round((avg04 / 4.0) * 100.0, 1), 0, 100);
            var comments = details
                .Where(d => !string.IsNullOrWhiteSpace(d.Evidence))
                .Select(d => d.Evidence!.Trim())
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            return new StudentAnonymousCriterionFeedback(cfg.ShortName, cfg.FullTitle, cfg.Weight, avgW, pct, comments);
        }).ToList();

        return new StudentFeedbackReport(
            evals.Count,
            avg,
            RubricDefinition.Classify(avg),
            avg >= cls.MinPassingScore,
            cls.MinPassingScore,
            criteriaFeedback);
    }

    public async Task DeleteEvaluationAsync(int evaluationId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await db.EvaluationDetails.Where(d => d.EvaluationId == evaluationId).ExecuteDeleteAsync();
        await db.Evaluations.Where(e => e.Id == evaluationId).ExecuteDeleteAsync();
    }

    public async Task DeleteEvaluationsForStudentAsync(int evaluatedStudentId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var evalIds = await db.Evaluations
            .Where(e => e.EvaluatedId == evaluatedStudentId)
            .Select(e => e.Id)
            .ToListAsync();

        if (evalIds.Count > 0)
        {
            await db.EvaluationDetails.Where(d => evalIds.Contains(d.EvaluationId)).ExecuteDeleteAsync();
            await db.Evaluations.Where(e => evalIds.Contains(e.Id)).ExecuteDeleteAsync();
        }
    }

    public async Task DeleteEvaluationsForGroupAsync(int groupId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var memberIds = await db.Students
            .Where(s => s.WorkGroupId == groupId)
            .Select(s => s.Id)
            .ToListAsync();

        if (memberIds.Count == 0) return;

        var evalIds = await db.Evaluations
            .Where(e => memberIds.Contains(e.EvaluatedId))
            .Select(e => e.Id)
            .ToListAsync();

        if (evalIds.Count > 0)
        {
            await db.EvaluationDetails.Where(d => evalIds.Contains(d.EvaluationId)).ExecuteDeleteAsync();
            await db.Evaluations.Where(e => evalIds.Contains(e.Id)).ExecuteDeleteAsync();
        }
    }

    public async Task DeleteAllEvaluationsForClassAsync(int classRoomId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var evalIds = await db.Evaluations
            .Where(e => e.ClassRoomId == classRoomId)
            .Select(e => e.Id)
            .ToListAsync();

        if (evalIds.Count > 0)
        {
            await db.EvaluationDetails.Where(d => evalIds.Contains(d.EvaluationId)).ExecuteDeleteAsync();
            await db.Evaluations.Where(e => evalIds.Contains(e.Id)).ExecuteDeleteAsync();
        }
    }

    public async Task<byte[]> ExportResultsExcelAsync(int classRoomId)
    {
        var summaries = await GetStudentSummariesAsync(classRoomId);
        var groupSummaries = await GetGroupSummariesAsync(classRoomId);
        var configs = await GetCriterionConfigsAsync(classRoomId);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var cls = await db.ClassRooms.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classRoomId);
        double minPassing = cls?.MinPassingScore ?? 70.0;

        using var wb = new XLWorkbook();

        // Sheet 1: Resumen Estudiantes
        var ws = wb.Worksheets.Add("Resumen Estudiantes");
        ws.Cell(1, 1).Value = "Matrícula";
        ws.Cell(1, 2).Value = "Nombre";
        ws.Cell(1, 3).Value = "Grupo";
        ws.Cell(1, 4).Value = "Carrera";
        ws.Cell(1, 5).Value = "Tema de Tesis";
        ws.Cell(1, 6).Value = "Evaluaciones Recibidas";
        ws.Cell(1, 7).Value = "Evaluaciones Realizadas";
        ws.Cell(1, 8).Value = "Promedio";
        ws.Cell(1, 9).Value = "Clasificación";
        ws.Cell(1, 10).Value = "Estado";

        int row = 2;
        foreach (var s in summaries)
        {
            ws.Cell(row, 1).Value = s.Student.Matricula;
            ws.Cell(row, 2).Value = s.Student.Nombre;
            ws.Cell(row, 3).Value = s.GroupName ?? "Individual";
            ws.Cell(row, 4).Value = s.Student.Carrera;
            ws.Cell(row, 5).Value = s.Student.TemaTesis;
            ws.Cell(row, 6).Value = s.EvaluationsReceivedCount;
            ws.Cell(row, 7).Value = s.EvaluationsGivenCount;
            ws.Cell(row, 8).Value = s.AverageScore;
            ws.Cell(row, 9).Value = s.Classification;
            ws.Cell(row, 10).Value = s.EvaluationsReceivedCount == 0 ? "Pendiente" : (s.Approved ? "Aprobado" : "Reprobado");
            row++;
        }
        ws.Columns().AdjustToContents();

        // Sheet 2: Resumen Grupos
        var wsGrp = wb.Worksheets.Add("Resumen Grupos");
        wsGrp.Cell(1, 1).Value = "Grupo";
        wsGrp.Cell(1, 2).Value = "Integrantes";
        wsGrp.Cell(1, 3).Value = "Tema";
        wsGrp.Cell(1, 4).Value = "Evaluadores Únicos";
        wsGrp.Cell(1, 5).Value = "Promedio";
        wsGrp.Cell(1, 6).Value = "Clasificación";
        wsGrp.Cell(1, 7).Value = "Estado";

        int gRow = 2;
        foreach (var g in groupSummaries)
        {
            wsGrp.Cell(gRow, 1).Value = g.Group.Name;
            wsGrp.Cell(gRow, 2).Value = string.Join(", ", g.Members.Select(m => $"{m.Nombre} ({m.Matricula})"));
            wsGrp.Cell(gRow, 3).Value = g.Group.Topic ?? "";
            wsGrp.Cell(gRow, 4).Value = g.UniqueEvaluatorsCount;
            wsGrp.Cell(gRow, 5).Value = g.AverageScore;
            wsGrp.Cell(gRow, 6).Value = g.Classification;
            wsGrp.Cell(gRow, 7).Value = g.UniqueEvaluatorsCount == 0 ? "Pendiente" : (g.Approved ? "Aprobado" : "Reprobado");
            gRow++;
        }
        wsGrp.Columns().AdjustToContents();

        // Sheet 3: Individual evaluations
        var allEvals = await db.Evaluations.AsNoTracking()
            .Include(e => e.Evaluated)
                .ThenInclude(s => s!.WorkGroup)
            .Include(e => e.Evaluator)
            .Include(e => e.Details)
            .Where(e => e.ClassRoomId == classRoomId)
            .OrderBy(e => e.Evaluated!.Matricula)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync();

        var wsDetail = wb.Worksheets.Add("Detalle Evaluaciones");
        wsDetail.Cell(1, 1).Value = "Matrícula Evaluado";
        wsDetail.Cell(1, 2).Value = "Nombre Evaluado";
        wsDetail.Cell(1, 3).Value = "Grupo";
        wsDetail.Cell(1, 4).Value = "Matrícula Evaluador";
        wsDetail.Cell(1, 5).Value = "Nombre Evaluador";
        wsDetail.Cell(1, 6).Value = "Puntaje Total";
        wsDetail.Cell(1, 7).Value = "Clasificación";
        wsDetail.Cell(1, 8).Value = "Estado";
        wsDetail.Cell(1, 9).Value = "Fecha (Hora RD)";

        int col = 10;
        foreach (var c in configs)
        {
            wsDetail.Cell(1, col++).Value = $"{c.ShortName} ({c.Weight}%)";
            wsDetail.Cell(1, col++).Value = $"Obs. {c.ShortName}";
        }

        int dRow = 2;
        foreach (var ev in allEvals)
        {
            wsDetail.Cell(dRow, 1).Value = ev.Evaluated?.Matricula ?? "";
            wsDetail.Cell(dRow, 2).Value = ev.Evaluated?.Nombre ?? "";
            wsDetail.Cell(dRow, 3).Value = ev.Evaluated?.WorkGroup?.Name ?? "Individual";
            wsDetail.Cell(dRow, 4).Value = ev.Evaluator?.Matricula ?? "";
            wsDetail.Cell(dRow, 5).Value = ev.Evaluator?.Nombre ?? "";
            wsDetail.Cell(dRow, 6).Value = ev.TotalScore;
            wsDetail.Cell(dRow, 7).Value = ev.Classification;
            wsDetail.Cell(dRow, 8).Value = ev.TotalScore >= minPassing ? "Aprobado" : "Reprobado";
            wsDetail.Cell(dRow, 9).Value = ev.CreatedAt.AddHours(-4).ToString("yyyy-MM-dd HH:mm");

            int cCol = 10;
            foreach (var c in configs)
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

    public async Task<(byte[] Bytes, string FileName)?> ExportStudentRubricExcelAsync(int evaluatedStudentId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var student = await db.Students.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == evaluatedStudentId);
        if (student == null) return null;

        var evaluations = await db.Evaluations.AsNoTracking()
            .Include(e => e.Details)
            .Where(e => e.EvaluatedId == evaluatedStudentId)
            .ToListAsync();
        if (evaluations.Count == 0) return null;

        var cls = await db.ClassRooms.AsNoTracking().FirstOrDefaultAsync(c => c.Id == student.ClassRoomId);
        if (cls == null) return null;

        var avgScores = ComputeAverageScores(evaluations);
        var configs = await GetCriterionConfigsAsync(student.ClassRoomId);
        var latestDateLocal = evaluations.Max(e => e.CreatedAt).AddHours(-4);

        var bytes = BuildRubricFromTemplate(
            student.Nombre,
            student.Matricula,
            student.TemaTesis,
            avgScores,
            latestDateLocal,
            cls,
            configs,
            memberCount: 1);

        var fileName = $"Rubrica_{SanitizeFilePart(student.Matricula)}_{SanitizeFilePart(student.Nombre)}.xlsx";
        return (bytes, fileName);
    }

    public async Task<(byte[] Bytes, string FileName)?> ExportSingleEvaluationRubricExcelAsync(int evaluationId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var eval = await db.Evaluations.AsNoTracking()
            .Include(e => e.Evaluated)
            .Include(e => e.Evaluator)
            .Include(e => e.Details)
            .FirstOrDefaultAsync(e => e.Id == evaluationId);
        if (eval?.Evaluated == null) return null;

        var cls = await db.ClassRooms.AsNoTracking().FirstOrDefaultAsync(c => c.Id == eval.ClassRoomId);
        if (cls == null) return null;

        var scores = new Dictionary<string, double>();
        foreach (var key in TemplateCriterionKeys)
        {
            var det = eval.Details.FirstOrDefault(d => d.CriterionKey == key);
            scores[key] = det != null ? det.Score : 0.0;
        }

        var configs = await GetCriterionConfigsAsync(eval.ClassRoomId);
        var evalDateLocal = eval.CreatedAt.AddHours(-4);

        var bytes = BuildRubricFromTemplate(
            eval.Evaluated.Nombre,
            eval.Evaluated.Matricula,
            eval.Evaluated.TemaTesis,
            scores,
            evalDateLocal,
            cls,
            configs,
            memberCount: 1);

        string evalTag = eval.Evaluator?.Matricula ?? eval.EvaluatorId.ToString();
        var fileName = $"Rubrica_{SanitizeFilePart(eval.Evaluated.Matricula)}_{SanitizeFilePart(eval.Evaluated.Nombre)}_Eval_{SanitizeFilePart(evalTag)}.xlsx";
        return (bytes, fileName);
    }

    public async Task<(byte[] Bytes, string FileName)?> ExportGroupRubricExcelAsync(int groupId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var group = await db.WorkGroups.AsNoTracking()
            .Include(g => g.Members.OrderBy(m => m.Nombre))
                .ThenInclude(m => m.EvaluationsReceived)
                    .ThenInclude(e => e.Details)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null || group.Members.Count == 0) return null;

        var allEvals = group.Members.SelectMany(m => m.EvaluationsReceived).ToList();
        if (allEvals.Count == 0) return null;

        var cls = await db.ClassRooms.AsNoTracking().FirstOrDefaultAsync(c => c.Id == group.ClassRoomId);
        if (cls == null) return null;

        var avgScores = ComputeAverageScores(allEvals);
        var configs = await GetCriterionConfigsAsync(group.ClassRoomId);
        var latestDateLocal = allEvals.Max(e => e.CreatedAt).AddHours(-4);

        var members = group.Members.OrderBy(m => m.Nombre).ToList();
        string namesJoined = string.Join("\n", members.Select(m => m.Nombre));
        string matriculasJoined = string.Join("\n", members.Select(m => m.Matricula));
        string topic = !string.IsNullOrWhiteSpace(group.Topic)
            ? group.Topic
            : members.First().TemaTesis;

        var bytes = BuildRubricFromTemplate(
            namesJoined,
            matriculasJoined,
            topic,
            avgScores,
            latestDateLocal,
            cls,
            configs,
            memberCount: members.Count);

        var fileName = $"Rubrica_Grupo_{SanitizeFilePart(group.Name)}.xlsx";
        return (bytes, fileName);
    }

    public async Task<(byte[] Bytes, string FileName)?> ExportAllRubricsZipAsync(int classRoomId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var cls = await db.ClassRooms.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classRoomId);
        if (cls == null) return null;

        var students = await db.Students.AsNoTracking()
            .Include(s => s.EvaluationsReceived)
                .ThenInclude(e => e.Details)
            .Where(s => s.ClassRoomId == classRoomId && s.EvaluationsReceived.Any())
            .OrderBy(s => s.Nombre)
            .ToListAsync();

        var groups = await db.WorkGroups.AsNoTracking()
            .Include(g => g.Members.OrderBy(m => m.Nombre))
                .ThenInclude(m => m.EvaluationsReceived)
                    .ThenInclude(e => e.Details)
            .Where(g => g.ClassRoomId == classRoomId)
            .OrderBy(g => g.Name)
            .ToListAsync();

        var evaluatedGroups = groups
            .Where(g => g.Members.Any(m => m.EvaluationsReceived.Any()))
            .ToList();

        if (students.Count == 0 && evaluatedGroups.Count == 0) return null;

        var configs = await GetCriterionConfigsAsync(classRoomId);

        using var zipMs = new MemoryStream();
        using (var archive = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var student in students)
            {
                var avgScores = ComputeAverageScores(student.EvaluationsReceived.ToList());
                var latestDateLocal = student.EvaluationsReceived.Max(e => e.CreatedAt).AddHours(-4);
                var xlsxBytes = BuildRubricFromTemplate(
                    student.Nombre,
                    student.Matricula,
                    student.TemaTesis,
                    avgScores,
                    latestDateLocal,
                    cls,
                    configs,
                    memberCount: 1);

                var entryName = $"Individuales/Rubrica_{SanitizeFilePart(student.Matricula)}_{SanitizeFilePart(student.Nombre)}.xlsx";
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                await entryStream.WriteAsync(xlsxBytes);
            }

            foreach (var grp in evaluatedGroups)
            {
                var members = grp.Members.OrderBy(m => m.Nombre).ToList();
                var allEvals = members.SelectMany(m => m.EvaluationsReceived).ToList();
                var avgScores = ComputeAverageScores(allEvals);
                var latestDateLocal = allEvals.Max(e => e.CreatedAt).AddHours(-4);
                string topic = !string.IsNullOrWhiteSpace(grp.Topic) ? grp.Topic : members.First().TemaTesis;

                var xlsxBytes = BuildRubricFromTemplate(
                    string.Join("\n", members.Select(m => m.Nombre)),
                    string.Join("\n", members.Select(m => m.Matricula)),
                    topic,
                    avgScores,
                    latestDateLocal,
                    cls,
                    configs,
                    memberCount: members.Count);

                var entryName = $"Grupos/Rubrica_Grupo_{SanitizeFilePart(grp.Name)}.xlsx";
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                await entryStream.WriteAsync(xlsxBytes);
            }
        }

        var zipFileName = $"Rubricas_{SanitizeFilePart(cls.Code)}_{DateTime.UtcNow.AddHours(-4):yyyyMMdd}.zip";
        return (zipMs.ToArray(), zipFileName);
    }

    private static Dictionary<string, double> ComputeAverageScores(List<Evaluation> evaluations)
    {
        var avgScores = new Dictionary<string, double>();
        foreach (var key in TemplateCriterionKeys)
        {
            var matching = evaluations
                .SelectMany(e => e.Details)
                .Where(d => d.CriterionKey == key)
                .ToList();
            avgScores[key] = matching.Count > 0 ? matching.Average(d => d.Score) : 0.0;
        }
        return avgScores;
    }

    private static List<int> ParseStudentIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<int>>(json) ?? new();
        }
        catch
        {
            return new();
        }
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
        string studentNames,
        string studentMatriculas,
        string thesisTopic,
        Dictionary<string, double> criterionScores0To4,
        DateTime dateLocal,
        ClassRoom cls,
        List<CriterionConfig> configs,
        int memberCount)
    {
        var templateBytes = File.ReadAllBytes(ResolveTemplatePath());
        using var templateMs = new MemoryStream(templateBytes);
        using var wb = new XLWorkbook(templateMs);
        var ws = wb.Worksheet("Rúbrica TFM");

        var titleText = string.IsNullOrWhiteSpace(cls.ProgramName)
            ? "RÚBRICA PARA LA EVALUACIÓN DE LOS TRABAJOS FINALES DE POSGRADO"
            : $"RÚBRICA PARA LA EVALUACIÓN DE LOS TRABAJOS FINALES DE POSGRADO\n({cls.ProgramName.Trim()})";
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

        ws.Cell("G28").Value = cls.MinPassingScore;

        // Footer: Nombre(s) (A33:B33)
        var nombreRange = ws.Range("A33:B33");
        nombreRange.Merge();
        var a33 = ws.Cell("A33");
        a33.Value = studentNames;
        a33.Style.Font.FontName = "Arial";
        a33.Style.Font.FontSize = 9;
        a33.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        a33.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        a33.Style.Alignment.WrapText = true;
        nombreRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        // Footer: Matrícula(s) (C33)
        var c33 = ws.Cell("C33");
        c33.Value = studentMatriculas;
        c33.Style.Font.FontName = "Arial";
        c33.Style.Font.FontSize = 9;
        c33.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        c33.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        c33.Style.Alignment.WrapText = true;
        c33.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        // Footer: Fecha (D33)
        var d33 = ws.Cell("D33");
        d33.Value = dateLocal.ToString("dd/MM/yyyy");
        d33.Style.Font.FontName = "Arial";
        d33.Style.Font.FontSize = 9;
        d33.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        d33.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        d33.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        if (memberCount > 1)
            ws.Row(33).Height = Math.Max(22, memberCount * 16);

        // Footer: Profesor (B35:C35)
        var profRange = ws.Range("B35:C35");
        profRange.Merge();
        var b35 = ws.Cell("B35");
        b35.Value = string.IsNullOrWhiteSpace(cls.TeacherName)
            ? "Ricardo Cesar Reinoso Rodriguez"
            : cls.TeacherName.Trim();
        b35.Style.Font.FontName = "Arial";
        b35.Style.Font.FontSize = 9;
        b35.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        b35.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        profRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        // Footer: Nombre del proyecto o caso (A38:G38)
        var temaRange = ws.Range("A38:G38");
        temaRange.Merge();
        var a38 = ws.Cell("A38");
        a38.Value = thesisTopic;
        a38.Style.Font.FontName = "Arial";
        a38.Style.Font.FontSize = 9;
        a38.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        a38.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        a38.Style.Alignment.WrapText = true;
        temaRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        if (thesisTopic.Length > 190)
            ws.Row(38).Height = 44;
        else if (thesisTopic.Length > 95)
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
