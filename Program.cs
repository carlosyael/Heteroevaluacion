using HeteroevaluacionApp.Components;
using HeteroevaluacionApp.Data;
using HeteroevaluacionApp.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? "Server=localhost;Port=3306;Database=heteroevaluacion;User=root;Password=rootpassword;";

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseMySQL(connectionString));

builder.Services.AddScoped<StudentImportService>();
builder.Services.AddScoped<EvaluationService>();
builder.Services.AddScoped<SessionService>();

var app = builder.Build();

// Ensure MySQL database & tables are created (with retry for Docker startup)
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    for (int attempt = 1; attempt <= 15; attempt++)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            await db.Database.EnsureCreatedAsync();

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS CriterionConfigs (
                    `Key` VARCHAR(50) NOT NULL PRIMARY KEY,
                    `Order` INT NOT NULL,
                    ShortName VARCHAR(60) NOT NULL,
                    FullTitle VARCHAR(150) NOT NULL,
                    ChapterTag VARCHAR(50) NOT NULL,
                    Weight DOUBLE NOT NULL,
                    IsActive TINYINT(1) NOT NULL DEFAULT 1
                );");

            // Safe column migrations for existing MySQL 8.x databases
            var conn = db.Database.GetDbConnection();
            bool openedHere = conn.State != System.Data.ConnectionState.Open;
            if (openedHere) await conn.OpenAsync();
            try
            {
                await EnsureColumnAsync(conn, "EvaluationStates", "TeacherName",
                    "VARCHAR(150) NOT NULL DEFAULT 'Ricardo Cesar Reinoso Rodriguez'");
                await EnsureColumnAsync(conn, "EvaluationStates", "ProgramName",
                    "VARCHAR(250) NOT NULL DEFAULT 'Maestría en Enseñanza de la Matemática en el Nivel Secundario'");
            }
            finally
            {
                if (openedHere) await conn.CloseAsync();
            }

            if (!db.Students.Any())
            {
                var evalService = scope.ServiceProvider.GetRequiredService<EvaluationService>();
                await evalService.SeedSampleDataAsync();
            }

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

static async Task EnsureColumnAsync(System.Data.Common.DbConnection conn, string tableName, string columnName, string columnDef)
{
    await using var checkCmd = conn.CreateCommand();
    checkCmd.CommandText = @"
        SELECT COUNT(*)
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @tbl
          AND COLUMN_NAME = @col;";
    var pTbl = checkCmd.CreateParameter();
    pTbl.ParameterName = "@tbl";
    pTbl.Value = tableName;
    checkCmd.Parameters.Add(pTbl);

    var pCol = checkCmd.CreateParameter();
    pCol.ParameterName = "@col";
    pCol.Value = columnName;
    checkCmd.Parameters.Add(pCol);

    var result = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
    if (result == 0)
    {
        await using var alterCmd = conn.CreateCommand();
        alterCmd.CommandText = $"ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {columnDef};";
        await alterCmd.ExecuteNonQueryAsync();
    }
}
