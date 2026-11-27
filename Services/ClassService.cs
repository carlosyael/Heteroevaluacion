using HeteroevaluacionApp.Data;
using HeteroevaluacionApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HeteroevaluacionApp.Services;

public class ClassService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public ClassService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<ClassRoom>> GetAllClassesAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.ClassRooms.AsNoTracking()
            .OrderBy(c => c.IsArchived)
            .ThenBy(c => c.Id)
            .ToListAsync();
    }

    public async Task<List<ClassRoom>> GetActiveClassesForStudentsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.ClassRooms.AsNoTracking()
            .Where(c => !c.IsArchived)
            .OrderBy(c => c.Id)
            .ToListAsync();
    }

    public async Task<ClassRoom?> GetClassByIdAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.ClassRooms.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<ClassRoom?> GetClassByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var norm = code.Trim().ToUpperInvariant();
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.ClassRooms.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == norm);
    }

    public async Task<(bool Success, string Message, ClassRoom? Created)> CreateClassAsync(
        string name,
        string? code,
        string teacherName,
        string programName,
        string? syllabus,
        double minPassingScore = 70.0)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "El nombre de la clase es obligatorio.", null);

        await using var db = await _dbFactory.CreateDbContextAsync();

        string finalCode = string.IsNullOrWhiteSpace(code)
            ? await GenerateUniqueCodeAsync(db, name)
            : NormalizeCode(code);

        if (string.IsNullOrWhiteSpace(finalCode))
            return (false, "El código de la clase no es válido.", null);

        if (await db.ClassRooms.AnyAsync(c => c.Code == finalCode))
            return (false, $"Ya existe una clase con el código '{finalCode}'.", null);

        var cls = new ClassRoom
        {
            Name = name.Trim(),
            Code = finalCode,
            TeacherName = string.IsNullOrWhiteSpace(teacherName) ? "Ricardo Cesar Reinoso Rodriguez" : teacherName.Trim(),
            ProgramName = programName.Trim(),
            Syllabus = string.IsNullOrWhiteSpace(syllabus) ? null : syllabus.Trim(),
            MinPassingScore = Math.Clamp(Math.Round(minPassingScore, 2), 0, 100),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.ClassRooms.Add(cls);
        await db.SaveChangesAsync();

        var defaultCriteria = RubricDefinition.Criteria.Select(c => new CriterionConfig
        {
            ClassRoomId = cls.Id,
            Key = c.Key,
            Order = c.Order,
            ShortName = c.ShortName,
            FullTitle = c.FullTitle,
            ChapterTag = c.ChapterTag,
            Weight = c.Weight,
            IsActive = true
        });
        db.CriterionConfigs.AddRange(defaultCriteria);
        await db.SaveChangesAsync();

        return (true, $"Clase '{cls.Name}' ({cls.Code}) creada correctamente.", cls);
    }

    public async Task<(bool Success, string Message)> UpdateClassDetailsAsync(
        int classId,
        string name,
        string code,
        string teacherName,
        string programName,
        string? syllabus,
        double minPassingScore,
        bool publishResultsToStudents = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "El nombre de la clase es obligatorio.");
        if (minPassingScore < 0 || minPassingScore > 100)
            return (false, "El mínimo aprobatorio debe estar entre 0 y 100.");

        var normCode = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(normCode))
            return (false, "El código de la clase no es válido.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var cls = await db.ClassRooms.FindAsync(classId);
        if (cls == null)
            return (false, "Clase no encontrada.");

        bool codeUsedByOther = await db.ClassRooms.AnyAsync(c => c.Id != classId && c.Code == normCode);
        if (codeUsedByOther)
            return (false, $"El código '{normCode}' ya está en uso por otra clase.");

        cls.Name = name.Trim();
        cls.Code = normCode;
        cls.TeacherName = string.IsNullOrWhiteSpace(teacherName) ? cls.TeacherName : teacherName.Trim();
        cls.ProgramName = programName.Trim();
        cls.Syllabus = string.IsNullOrWhiteSpace(syllabus) ? null : syllabus.Trim();
        cls.MinPassingScore = Math.Round(minPassingScore, 2);
        cls.PublishResultsToStudents = publishResultsToStudents;
        cls.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return (true, "Datos de la clase actualizados correctamente.");
    }

    public async Task SetPublishResultsAsync(int classId, bool publish)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var cls = await db.ClassRooms.FindAsync(classId);
        if (cls != null)
        {
            cls.PublishResultsToStudents = publish;
            cls.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public async Task<(bool Success, string Message)> SetClassArchivedAsync(int classId, bool isArchived)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var cls = await db.ClassRooms.FindAsync(classId);
        if (cls == null)
            return (false, "Clase no encontrada.");

        cls.IsArchived = isArchived;
        if (isArchived)
        {
            cls.IsEvaluationOpen = false;
            cls.RoundEndsAtUtc = null;
        }
        cls.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, isArchived ? $"Clase '{cls.Name}' archivada." : $"Clase '{cls.Name}' reactivada.");
    }

    public async Task<(bool Success, string Message, ClassRoom? Created)> DuplicateClassAsync(int sourceClassId, string? customName = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var src = await db.ClassRooms.AsNoTracking().FirstOrDefaultAsync(c => c.Id == sourceClassId);
        if (src == null)
            return (false, "Clase origen no encontrada.", null);

        var rawName = string.IsNullOrWhiteSpace(customName) ? $"{src.Name} (Copia)" : customName.Trim();
        if (rawName.Length > 150) rawName = rawName[..150].Trim();
        var newCode = await GenerateUniqueCodeAsync(db, rawName);

        var clone = new ClassRoom
        {
            Code = newCode,
            Name = rawName,
            TeacherName = src.TeacherName,
            ProgramName = src.ProgramName,
            Syllabus = src.Syllabus,
            MinPassingScore = src.MinPassingScore,
            IsArchived = false,
            PublishResultsToStudents = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.ClassRooms.Add(clone);
        await db.SaveChangesAsync();

        var srcCriteria = await db.CriterionConfigs.AsNoTracking()
            .Where(c => c.ClassRoomId == sourceClassId)
            .OrderBy(c => c.Order)
            .ToListAsync();

        var clonedCriteria = srcCriteria.Count > 0
            ? srcCriteria.Select(c => new CriterionConfig
            {
                ClassRoomId = clone.Id,
                Key = c.Key,
                Order = c.Order,
                ShortName = c.ShortName,
                FullTitle = c.FullTitle,
                ChapterTag = c.ChapterTag,
                Weight = c.Weight,
                IsActive = c.IsActive
            }).ToList()
            : RubricDefinition.Criteria.Select(c => new CriterionConfig
            {
                ClassRoomId = clone.Id,
                Key = c.Key,
                Order = c.Order,
                ShortName = c.ShortName,
                FullTitle = c.FullTitle,
                ChapterTag = c.ChapterTag,
                Weight = c.Weight,
                IsActive = true
            }).ToList();

        db.CriterionConfigs.AddRange(clonedCriteria);
        await db.SaveChangesAsync();

        return (true, $"Clase duplicada como '{clone.Name}' ({clone.Code}).", clone);
    }

    public async Task AssignStudentGroupAsync(int classId, int studentId, int? workGroupId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var student = await db.Students.FirstOrDefaultAsync(s => s.ClassRoomId == classId && s.Id == studentId);
        if (student == null) return;

        if (workGroupId.HasValue)
        {
            bool groupValid = await db.WorkGroups.AnyAsync(g => g.Id == workGroupId.Value && g.ClassRoomId == classId);
            if (!groupValid) return;
        }

        student.WorkGroupId = workGroupId;
        await db.SaveChangesAsync();
    }

    public async Task<(bool Success, string Message)> DeleteClassAsync(int classId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        int total = await db.ClassRooms.CountAsync();
        if (total <= 1)
            return (false, "Debe existir al menos una clase activa en el sistema.");

        var cls = await db.ClassRooms.FindAsync(classId);
        if (cls == null)
            return (false, "Clase no encontrada.");

        await DeleteClassEvaluationsAsync(db, classId);
        await db.Students.Where(s => s.ClassRoomId == classId).ExecuteDeleteAsync();
        await db.WorkGroups.Where(g => g.ClassRoomId == classId).ExecuteDeleteAsync();
        await db.CriterionConfigs.Where(c => c.ClassRoomId == classId).ExecuteDeleteAsync();
        db.ClassRooms.Remove(cls);
        await db.SaveChangesAsync();

        return (true, "Clase eliminada correctamente.");
    }

    public async Task ResetClassDataAsync(int classId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await DeleteClassEvaluationsAsync(db, classId);
        await db.Students.Where(s => s.ClassRoomId == classId).ExecuteDeleteAsync();
        await db.WorkGroups.Where(g => g.ClassRoomId == classId).ExecuteDeleteAsync();

        var cls = await db.ClassRooms.FindAsync(classId);
        if (cls != null)
        {
            cls.IsEvaluationOpen = false;
            cls.RoundEndsAtUtc = null;
            cls.ActiveGroupId = null;
            cls.ActiveGroupName = null;
            cls.ActiveStudentIdsJson = null;
            cls.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Deletes evaluation details by explicit IDs to avoid MySQL Error 1093 (subquery referencing target table in DELETE).
    /// </summary>
    private static async Task DeleteClassEvaluationsAsync(AppDbContext db, int classId)
    {
        var evalIds = await db.Evaluations
            .Where(e => e.ClassRoomId == classId)
            .Select(e => e.Id)
            .ToListAsync();

        if (evalIds.Count > 0)
        {
            await db.EvaluationDetails.Where(d => evalIds.Contains(d.EvaluationId)).ExecuteDeleteAsync();
            await db.Evaluations.Where(e => evalIds.Contains(e.Id)).ExecuteDeleteAsync();
        }
    }

    public async Task<List<WorkGroup>> GetGroupsForClassAsync(int classId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.WorkGroups.AsNoTracking()
            .Include(g => g.Members.OrderBy(m => m.Nombre))
            .Where(g => g.ClassRoomId == classId)
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> SaveGroupAsync(
        int classId,
        int? groupId,
        string name,
        string? topic,
        List<int> memberStudentIds)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "El nombre del grupo es obligatorio.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        WorkGroup? group;
        if (groupId.HasValue)
        {
            group = await db.WorkGroups.FirstOrDefaultAsync(g => g.Id == groupId.Value && g.ClassRoomId == classId);
            if (group == null) return (false, "Grupo no encontrado.");
            group.Name = name.Trim();
            group.Topic = string.IsNullOrWhiteSpace(topic) ? null : topic.Trim();
        }
        else
        {
            group = new WorkGroup
            {
                ClassRoomId = classId,
                Name = name.Trim(),
                Topic = string.IsNullOrWhiteSpace(topic) ? null : topic.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            db.WorkGroups.Add(group);
            await db.SaveChangesAsync();
        }

        var selectedSet = memberStudentIds.ToHashSet();

        // Unassign removed members currently in this group
        var currentMembers = await db.Students
            .Where(s => s.ClassRoomId == classId && s.WorkGroupId == group.Id)
            .ToListAsync();
        foreach (var m in currentMembers)
        {
            if (!selectedSet.Contains(m.Id))
                m.WorkGroupId = null;
        }

        // Assign selected members (enforces 1 group per student by overwriting previous WorkGroupId)
        if (selectedSet.Count > 0)
        {
            var toAssign = await db.Students
                .Where(s => s.ClassRoomId == classId && selectedSet.Contains(s.Id))
                .ToListAsync();
            foreach (var s in toAssign)
            {
                s.WorkGroupId = group.Id;
            }
        }

        await db.SaveChangesAsync();
        return (true, $"Grupo '{group.Name}' guardado correctamente.");
    }

    public async Task DeleteGroupAsync(int groupId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var members = await db.Students.Where(s => s.WorkGroupId == groupId).ToListAsync();
        foreach (var m in members)
            m.WorkGroupId = null;

        var group = await db.WorkGroups.FindAsync(groupId);
        if (group != null)
            db.WorkGroups.Remove(group);

        await db.SaveChangesAsync();
    }

    public async Task<int> AutoGroupByThesisTopicAsync(int classId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var ungrouped = await db.Students
            .Where(s => s.ClassRoomId == classId && s.WorkGroupId == null)
            .ToListAsync();

        var clusters = ungrouped
            .Where(s => !string.IsNullOrWhiteSpace(s.TemaTesis) && !s.TemaTesis.Equals("Sin especificar", StringComparison.OrdinalIgnoreCase))
            .GroupBy(s => s.TemaTesis.Trim().ToLowerInvariant())
            .Where(g => g.Count() >= 2)
            .ToList();

        if (clusters.Count == 0) return 0;

        int existingCount = await db.WorkGroups.CountAsync(g => g.ClassRoomId == classId);
        int created = 0;

        foreach (var cluster in clusters)
        {
            existingCount++;
            var group = new WorkGroup
            {
                ClassRoomId = classId,
                Name = $"Grupo {existingCount}",
                Topic = cluster.First().TemaTesis.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            db.WorkGroups.Add(group);
            await db.SaveChangesAsync();

            foreach (var student in cluster)
                student.WorkGroupId = group.Id;

            created++;
        }

        await db.SaveChangesAsync();
        return created;
    }

    private static string NormalizeCode(string raw)
    {
        var chars = raw.Trim().ToUpperInvariant()
            .Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-')
            .ToArray();
        var joined = new string(chars);
        while (joined.Contains("--")) joined = joined.Replace("--", "-");
        return joined.Trim('-');
    }

    private static async Task<string> GenerateUniqueCodeAsync(AppDbContext db, string name)
    {
        var prefix = NormalizeCode(name);
        if (prefix.Length > 14) prefix = prefix[..14].Trim('-');
        if (string.IsNullOrWhiteSpace(prefix)) prefix = "CLASE";

        int seq = await db.ClassRooms.CountAsync() + 1;
        string candidate = $"{prefix}-{seq:00}";
        while (await db.ClassRooms.AnyAsync(c => c.Code == candidate))
        {
            seq++;
            candidate = $"{prefix}-{seq:00}";
        }
        return candidate;
    }
}
