using System.Data;
using System.Data.Common;
using HeteroevaluacionApp.Components;
using HeteroevaluacionApp.Data;
using HeteroevaluacionApp.Models;
using HeteroevaluacionApp.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? "Server=localhost;Port=3306;Database=heteroevaluacion;User=root;Password=rootpassword;";

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseMySQL(connectionString));

builder.Services.AddScoped<ClassService>();
builder.Services.AddScoped<StudentImportService>();
builder.Services.AddScoped<EvaluationService>();
builder.Services.AddScoped<SessionService>();

var keysDir = Path.Combine(builder.Environment.ContentRootPath, "dataprotection-keys");
Directory.CreateDirectory(keysDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
    .SetApplicationName("HeteroevaluacionApp");

var app = builder.Build();

// Initialize / migrate MySQL database (supports fresh DBs and v10 -> v11 zero-data-loss upgrade)
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    for (int attempt = 1; attempt <= 15; attempt++)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            await InitializeAndMigrateAsync(db, logger);
            logger.LogInformation("Database initialized successfully.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Waiting for MySQL (attempt {Attempt}/15): {Message}", attempt, ex.Message);
            await Task.Delay(3000);
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static async Task InitializeAndMigrateAsync(AppDbContext db, ILogger logger)
{
    var conn = db.Database.GetDbConnection();
    bool openedHere = conn.State != ConnectionState.Open;
    if (openedHere) await conn.OpenAsync();

    bool needsLegacyMigration = false;
    try
    {
        bool studentsExists = await TableExistsAsync(conn, "Students");
        bool hasMatricula = studentsExists && await ColumnExistsAsync(conn, "Students", "Matricula");
        bool hasLegacyTables =
            await TableExistsAsync(conn, "_legacy_Students") ||
            await TableExistsAsync(conn, "_legacy_Evaluations") ||
            await TableExistsAsync(conn, "_legacy_EvaluationStates") ||
            await TableExistsAsync(conn, "_legacy_CriterionConfigs") ||
            await TableExistsAsync(conn, "_legacy_EvaluationDetails");
        needsLegacyMigration = (studentsExists && !hasMatricula) || hasLegacyTables;
    }
    finally
    {
        if (openedHere) await conn.CloseAsync();
    }

    if (needsLegacyMigration)
    {
        logger.LogInformation("Migrating v10 database schema to multi-class v11 schema...");
        await MigrateLegacyV10Async(db, logger);
        await EnsureLatestSchemaColumnsAsync(db);
        return;
    }

    await db.Database.EnsureCreatedAsync();
    await EnsureLatestSchemaColumnsAsync(db);

    if (!await db.AppSettings.AnyAsync(a => a.Id == 1))
    {
        var defaultPin = Environment.GetEnvironmentVariable("TEACHER_PIN") ?? "profesor2026";
        db.AppSettings.Add(new AppSetting { Id = 1, TeacherPin = defaultPin, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    if (!await db.ClassRooms.AnyAsync())
    {
        await SeedInitialClassAsync(db);
    }
}

static async Task MigrateLegacyV10Async(AppDbContext db, ILogger logger)
{
    var conn = db.Database.GetDbConnection();
    var legacyNames = new[] { "EvaluationDetails", "Evaluations", "Students", "CriterionConfigs", "EvaluationStates" };
    var allV11AndOldTables = new[] { "EvaluationDetails", "Evaluations", "Students", "WorkGroups", "CriterionConfigs", "ClassRooms", "AppSettings", "EvaluationStates" };
    var existingLegacyTables = new List<string>();

    string teacherPin = Environment.GetEnvironmentVariable("TEACHER_PIN") ?? "profesor2026";
    string teacherName = "Ricardo Cesar Reinoso Rodriguez";
    string programName = "Maestría en Enseñanza de la Matemática en el Nivel Secundario";
    double minPassing = 70.0;

    var legacyCriteria = new List<(string Key, int Order, string ShortName, string FullTitle, string ChapterTag, double Weight, bool IsActive)>();
    var legacyStudents = new List<(string Id, string Nombre, string Carrera, string TemaTesis, DateTime CreatedAt)>();
    var legacyEvals = new List<(int Id, string EvaluatorId, string EvaluatedId, double TotalScore, string Classification, bool Approved, DateTime CreatedAt)>();
    var legacyDetails = new List<(int EvaluationId, string CriterionKey, int Score, double WeightedPoints, string? Evidence)>();

    // Phase 1: Preserve / recover _legacy_* tables, drop partial v11 tables and old schema-wide FK names, and read legacy rows into memory
    if (conn.State != ConnectionState.Open) await conn.OpenAsync();
    try
    {
        bool isFreshV10 =
            !await TableExistsAsync(conn, "_legacy_Students") &&
            await TableExistsAsync(conn, "Students") &&
            !await ColumnExistsAsync(conn, "Students", "Matricula");

        await ExecuteSqlAsync(conn, "SET FOREIGN_KEY_CHECKS = 0;");
        try
        {
            if (isFreshV10)
            {
                foreach (var tbl in legacyNames)
                {
                    if (await TableExistsAsync(conn, tbl))
                    {
                        var legacyName = $"_legacy_{tbl}";
                        if (await TableExistsAsync(conn, legacyName))
                            await ExecuteSqlAsync(conn, $"DROP TABLE `{legacyName}`;");
                        await ExecuteSqlAsync(conn, $"RENAME TABLE `{tbl}` TO `{legacyName}`;");
                    }
                }
            }

            foreach (var tbl in allV11AndOldTables)
                await ExecuteSqlAsync(conn, $"DROP TABLE IF EXISTS `{tbl}`;");

            var fksToDrop = new List<(string TableName, string ConstraintName)>();
            await using (var fkCmd = conn.CreateCommand())
            {
                fkCmd.CommandText = @"
                    SELECT TABLE_NAME, CONSTRAINT_NAME
                    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
                    WHERE CONSTRAINT_SCHEMA = DATABASE()
                      AND CONSTRAINT_TYPE = 'FOREIGN KEY'
                      AND TABLE_NAME IN ('_legacy_EvaluationDetails', '_legacy_Evaluations', '_legacy_Students', '_legacy_CriterionConfigs', '_legacy_EvaluationStates');";
                await using var rdr = await fkCmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                    fksToDrop.Add((rdr.GetString(0), rdr.GetString(1)));
            }

            foreach (var (tbl, fk) in fksToDrop)
                await ExecuteSqlAsync(conn, $"ALTER TABLE `{tbl}` DROP FOREIGN KEY `{fk}`;");
        }
        finally
        {
            await ExecuteSqlAsync(conn, "SET FOREIGN_KEY_CHECKS = 1;");
        }

        foreach (var tbl in legacyNames)
        {
            var legacyName = $"_legacy_{tbl}";
            if (await TableExistsAsync(conn, legacyName))
                existingLegacyTables.Add(legacyName);
        }

        if (existingLegacyTables.Contains("_legacy_EvaluationStates"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT TeacherPin, TeacherName, ProgramName, MinPassingScore FROM `_legacy_EvaluationStates` WHERE Id = 1 LIMIT 1;";
            await using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                if (!rdr.IsDBNull(0)) teacherPin = rdr.GetString(0);
                if (!rdr.IsDBNull(1)) teacherName = rdr.GetString(1);
                if (!rdr.IsDBNull(2)) programName = rdr.GetString(2);
                if (!rdr.IsDBNull(3)) minPassing = rdr.GetDouble(3);
            }
        }

        if (existingLegacyTables.Contains("_legacy_CriterionConfigs"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT `Key`, `Order`, ShortName, FullTitle, ChapterTag, Weight, IsActive FROM `_legacy_CriterionConfigs`;";
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                legacyCriteria.Add((
                    rdr.GetString(0),
                    rdr.GetInt32(1),
                    rdr.GetString(2),
                    rdr.GetString(3),
                    rdr.GetString(4),
                    rdr.GetDouble(5),
                    rdr.GetBoolean(6)
                ));
            }
        }

        if (existingLegacyTables.Contains("_legacy_Students"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Nombre, Carrera, TemaTesis, CreatedAt FROM `_legacy_Students`;";
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                legacyStudents.Add((
                    rdr.GetString(0),
                    rdr.GetString(1),
                    rdr.GetString(2),
                    rdr.GetString(3),
                    rdr.IsDBNull(4) ? DateTime.UtcNow : rdr.GetDateTime(4)
                ));
            }
        }

        if (existingLegacyTables.Contains("_legacy_Evaluations"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, EvaluatorId, EvaluatedId, TotalScore, Classification, Approved, CreatedAt FROM `_legacy_Evaluations`;";
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                legacyEvals.Add((
                    rdr.GetInt32(0),
                    rdr.GetString(1),
                    rdr.GetString(2),
                    rdr.GetDouble(3),
                    rdr.GetString(4),
                    rdr.GetBoolean(5),
                    rdr.IsDBNull(6) ? DateTime.UtcNow : rdr.GetDateTime(6)
                ));
            }
        }

        if (existingLegacyTables.Contains("_legacy_EvaluationDetails"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT EvaluationId, CriterionKey, Score, WeightedPoints, Evidence FROM `_legacy_EvaluationDetails`;";
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                legacyDetails.Add((
                    rdr.GetInt32(0),
                    rdr.GetString(1),
                    rdr.GetInt32(2),
                    rdr.GetDouble(3),
                    rdr.IsDBNull(4) ? null : rdr.GetString(4)
                ));
            }
        }
    }
    finally
    {
        if (conn.State == ConnectionState.Open) await conn.CloseAsync();
    }

    // Phase 2: Create all 7 v11 tables cleanly (FK names on _legacy_* were already removed)
    var creator = db.Database.GetService<IRelationalDatabaseCreator>();
    await creator.CreateTablesAsync();

    // Phase 3: Insert migrated data via EF Core
    db.AppSettings.Add(new AppSetting { Id = 1, TeacherPin = teacherPin, UpdatedAt = DateTime.UtcNow });
    var class1 = new ClassRoom
    {
        Code = "CLASE-01",
        Name = "Clase Principal",
        TeacherName = teacherName,
        ProgramName = programName,
        MinPassingScore = minPassing,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    db.ClassRooms.Add(class1);
    await db.SaveChangesAsync();

    if (legacyCriteria.Count > 0)
    {
        foreach (var lc in legacyCriteria)
        {
            db.CriterionConfigs.Add(new CriterionConfig
            {
                ClassRoomId = class1.Id,
                Key = lc.Key,
                Order = lc.Order,
                ShortName = lc.ShortName,
                FullTitle = lc.FullTitle,
                ChapterTag = lc.ChapterTag,
                Weight = lc.Weight,
                IsActive = lc.IsActive
            });
        }
    }
    else
    {
        foreach (var c in RubricDefinition.Criteria)
        {
            db.CriterionConfigs.Add(new CriterionConfig
            {
                ClassRoomId = class1.Id,
                Key = c.Key,
                Order = c.Order,
                ShortName = c.ShortName,
                FullTitle = c.FullTitle,
                ChapterTag = c.ChapterTag,
                Weight = c.Weight,
                IsActive = true
            });
        }
    }
    await db.SaveChangesAsync();

    var studentIdMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var trackedStudents = new List<(string Matricula, Student Entity)>();
    foreach (var ls in legacyStudents)
    {
        var st = new Student
        {
            ClassRoomId = class1.Id,
            Matricula = ls.Id,
            Nombre = ls.Nombre,
            Carrera = ls.Carrera,
            TemaTesis = ls.TemaTesis,
            CreatedAt = ls.CreatedAt
        };
        db.Students.Add(st);
        trackedStudents.Add((ls.Id, st));
    }
    await db.SaveChangesAsync();
    foreach (var (matricula, entity) in trackedStudents)
        studentIdMap[matricula] = entity.Id;

    var evalIdMap = new Dictionary<int, int>();
    var trackedEvals = new List<(int OldId, Evaluation Entity)>();
    foreach (var le in legacyEvals)
    {
        if (!studentIdMap.TryGetValue(le.EvaluatorId, out int newEvaltrId) ||
            !studentIdMap.TryGetValue(le.EvaluatedId, out int newEvaldId))
            continue;

        var ev = new Evaluation
        {
            ClassRoomId = class1.Id,
            EvaluatorId = newEvaltrId,
            EvaluatedId = newEvaldId,
            TotalScore = le.TotalScore,
            Classification = le.Classification,
            Approved = le.Approved,
            CreatedAt = le.CreatedAt
        };
        db.Evaluations.Add(ev);
        trackedEvals.Add((le.Id, ev));
    }
    await db.SaveChangesAsync();
    foreach (var (oldId, entity) in trackedEvals)
        evalIdMap[oldId] = entity.Id;

    foreach (var ld in legacyDetails)
    {
        if (!evalIdMap.TryGetValue(ld.EvaluationId, out int newEvalId))
            continue;

        db.EvaluationDetails.Add(new EvaluationDetail
        {
            EvaluationId = newEvalId,
            CriterionKey = ld.CriterionKey,
            Score = ld.Score,
            WeightedPoints = ld.WeightedPoints,
            Evidence = ld.Evidence
        });
    }
    await db.SaveChangesAsync();

    // Phase 4: Drop _legacy_* tables only after Phase 3 completes cleanly
    if (conn.State != ConnectionState.Open) await conn.OpenAsync();
    try
    {
        await ExecuteSqlAsync(conn, "SET FOREIGN_KEY_CHECKS = 0;");
        try
        {
            foreach (var legacyName in existingLegacyTables)
                await ExecuteSqlAsync(conn, $"DROP TABLE IF EXISTS `{legacyName}`;");
        }
        finally
        {
            await ExecuteSqlAsync(conn, "SET FOREIGN_KEY_CHECKS = 1;");
        }
    }
    finally
    {
        if (conn.State == ConnectionState.Open) await conn.CloseAsync();
    }

    logger.LogInformation(
        "Legacy migration completed: {Students} students, {Evals} evaluations preserved in '{Code}'.",
        studentIdMap.Count, evalIdMap.Count, class1.Code);
}

static async Task SeedInitialClassAsync(AppDbContext db)
{
    var cls = new ClassRoom
    {
        Code = "CLASE-01",
        Name = "Seminario de Tesis I",
        TeacherName = "Ricardo Cesar Reinoso Rodriguez",
        ProgramName = "Maestría en Enseñanza de la Matemática en el Nivel Secundario",
        Syllabus = "Unidad 1: Planteamiento del problema y justificación\nUnidad 2: Revisión de literatura y marco teórico\nUnidad 3: Diseño metodológico e instrumentos\nUnidad 4: Análisis de resultados, conclusiones y formato APA 7",
        MinPassingScore = 70.0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    db.ClassRooms.Add(cls);
    await db.SaveChangesAsync();

    foreach (var c in RubricDefinition.Criteria)
    {
        db.CriterionConfigs.Add(new CriterionConfig
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
    }

    var g1 = new WorkGroup
    {
        ClassRoomId = cls.Id,
        Name = "Grupo 1",
        Topic = "Sistema de detección de anomalías con Machine Learning"
    };
    var g2 = new WorkGroup
    {
        ClassRoomId = cls.Id,
        Name = "Grupo 2",
        Topic = "Arquitectura de microservicios para plataformas educativas"
    };
    db.WorkGroups.AddRange(g1, g2);
    await db.SaveChangesAsync();

    db.Students.AddRange(
        new Student { ClassRoomId = cls.Id, Matricula = "20210001", Nombre = "Ana García López", Carrera = "Ingeniería en Sistemas", TemaTesis = g1.Topic!, WorkGroupId = g1.Id },
        new Student { ClassRoomId = cls.Id, Matricula = "20210002", Nombre = "Carlos Martínez Ruiz", Carrera = "Ingeniería Industrial", TemaTesis = g1.Topic!, WorkGroupId = g1.Id },
        new Student { ClassRoomId = cls.Id, Matricula = "20210003", Nombre = "María Fernández Díaz", Carrera = "Ingeniería en Software", TemaTesis = g2.Topic!, WorkGroupId = g2.Id },
        new Student { ClassRoomId = cls.Id, Matricula = "20210004", Nombre = "José Rodríguez Pérez", Carrera = "Ingeniería en Sistemas", TemaTesis = g2.Topic!, WorkGroupId = g2.Id },
        new Student { ClassRoomId = cls.Id, Matricula = "20210005", Nombre = "Lucía Sánchez Gómez", Carrera = "Ingeniería Industrial", TemaTesis = "Implementación de Lean Manufacturing en PYMES" },
        new Student { ClassRoomId = cls.Id, Matricula = "20210006", Nombre = "Diego Torres Castro", Carrera = "Ingeniería en Software", TemaTesis = "Seguridad en aplicaciones web con autenticación sin contraseña" }
    );
    await db.SaveChangesAsync();
}

static async Task EnsureLatestSchemaColumnsAsync(AppDbContext db)
{
    var conn = db.Database.GetDbConnection();
    bool openedHere = conn.State != ConnectionState.Open;
    if (openedHere) await conn.OpenAsync();
    try
    {
        if (!await ColumnExistsAsync(conn, "ClassRooms", "IsArchived"))
            await ExecuteSqlAsync(conn, "ALTER TABLE `ClassRooms` ADD COLUMN `IsArchived` tinyint(1) NOT NULL DEFAULT 0;");
        if (!await ColumnExistsAsync(conn, "ClassRooms", "PublishResultsToStudents"))
            await ExecuteSqlAsync(conn, "ALTER TABLE `ClassRooms` ADD COLUMN `PublishResultsToStudents` tinyint(1) NOT NULL DEFAULT 0;");
        if (!await ColumnExistsAsync(conn, "ClassRooms", "RoundEndsAtUtc"))
            await ExecuteSqlAsync(conn, "ALTER TABLE `ClassRooms` ADD COLUMN `RoundEndsAtUtc` datetime(6) NULL;");
    }
    finally
    {
        if (openedHere) await conn.CloseAsync();
    }
}

static async Task<bool> TableExistsAsync(DbConnection conn, string tableName)
{
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tbl;";
    var p = cmd.CreateParameter();
    p.ParameterName = "@tbl";
    p.Value = tableName;
    cmd.Parameters.Add(p);
    return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
}

static async Task<bool> ColumnExistsAsync(DbConnection conn, string tableName, string columnName)
{
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tbl AND COLUMN_NAME = @col;";
    var pTbl = cmd.CreateParameter();
    pTbl.ParameterName = "@tbl";
    pTbl.Value = tableName;
    cmd.Parameters.Add(pTbl);
    var pCol = cmd.CreateParameter();
    pCol.ParameterName = "@col";
    pCol.Value = columnName;
    cmd.Parameters.Add(pCol);
    return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
}

static async Task ExecuteSqlAsync(DbConnection conn, string sql)
{
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    await cmd.ExecuteNonQueryAsync();
}
