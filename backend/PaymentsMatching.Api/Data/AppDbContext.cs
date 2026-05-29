using Microsoft.EntityFrameworkCore;
using PaymentsMatching.Api.Models;

namespace PaymentsMatching.Api.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<MatchResult> MatchResults => Set<MatchResult>();

    public DbSet<ReconciliationSession> ReconciliationSessions
        => Set<ReconciliationSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // =========================
        // ReconciliationSession
        // =========================
        modelBuilder.Entity<ReconciliationSession>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CreatedAt).IsRequired();

            entity.Property(e => e.File1Name).HasMaxLength(255);

            entity.Property(e => e.File2Name).HasMaxLength(255);
        });

        // =========================
        // MatchResult
        // =========================
        modelBuilder.Entity<MatchResult>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Unique only inside a session
            entity.HasIndex(e => new
            {
                e.SessionId,
                e.OrderId,
                e.Currency
            }).IsUnique();

            entity.Property(e => e.OrderId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Currency)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(e => e.Status)
                .HasConversion<string>();

            entity.Property(e => e.ResolutionSide)
                .HasConversion<string?>();

            // Relationship
            entity.HasOne(e => e.Session)
                .WithMany(s => s.MatchResults)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}