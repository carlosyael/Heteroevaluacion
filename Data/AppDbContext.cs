using HeteroevaluacionApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HeteroevaluacionApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<EvaluationDetail> EvaluationDetails => Set<EvaluationDetail>();
    public DbSet<EvaluationState> EvaluationStates => Set<EvaluationState>();
    public DbSet<CriterionConfig> CriterionConfigs => Set<CriterionConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(s => s.Id);
        });

        modelBuilder.Entity<Evaluation>(entity =>
        {
            entity.HasKey(e => e.Id);

            // A student can only evaluate another specific student once
            entity.HasIndex(e => new { e.EvaluatorId, e.EvaluatedId }).IsUnique();

            entity.HasOne(e => e.Evaluator)
                .WithMany(s => s.EvaluationsGiven)
                .HasForeignKey(e => e.EvaluatorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Evaluated)
                .WithMany(s => s.EvaluationsReceived)
                .HasForeignKey(e => e.EvaluatedId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EvaluationDetail>(entity =>
        {
            entity.HasKey(d => d.Id);

            entity.HasOne(d => d.Evaluation)
                .WithMany(e => e.Details)
                .HasForeignKey(d => d.EvaluationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
