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
