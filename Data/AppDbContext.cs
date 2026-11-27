using HeteroevaluacionApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HeteroevaluacionApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<ClassRoom> ClassRooms => Set<ClassRoom>();
    public DbSet<WorkGroup> WorkGroups => Set<WorkGroup>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<CriterionConfig> CriterionConfigs => Set<CriterionConfig>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<EvaluationDetail> EvaluationDetails => Set<EvaluationDetail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(a => a.Id);
        });

        modelBuilder.Entity<ClassRoom>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.Code).IsUnique();
        });

        modelBuilder.Entity<WorkGroup>(entity =>
        {
            entity.HasKey(g => g.Id);

            entity.HasOne(g => g.ClassRoom)
                .WithMany(c => c.WorkGroups)
                .HasForeignKey(g => g.ClassRoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => new { s.ClassRoomId, s.Matricula }).IsUnique();

            entity.HasOne(s => s.ClassRoom)
                .WithMany(c => c.Students)
                .HasForeignKey(s => s.ClassRoomId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.WorkGroup)
                .WithMany(g => g.Members)
                .HasForeignKey(s => s.WorkGroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CriterionConfig>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => new { c.ClassRoomId, c.Key }).IsUnique();

            entity.HasOne(c => c.ClassRoom)
                .WithMany(cl => cl.CriterionConfigs)
                .HasForeignKey(c => c.ClassRoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Evaluation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ClassRoomId, e.EvaluatorId, e.EvaluatedId }).IsUnique();

            entity.HasOne(e => e.ClassRoom)
                .WithMany(c => c.Evaluations)
                .HasForeignKey(e => e.ClassRoomId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Evaluator)
                .WithMany(s => s.EvaluationsGiven)
                .HasForeignKey(e => e.EvaluatorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Evaluated)
                .WithMany(s => s.EvaluationsReceived)
                .HasForeignKey(e => e.EvaluatedId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.WorkGroup)
                .WithMany()
                .HasForeignKey(e => e.WorkGroupId)
                .OnDelete(DeleteBehavior.SetNull);
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
