using Microsoft.EntityFrameworkCore;
using MOT.Domain.Entities;

using MOT.Application.Common.Interfaces;

namespace MOT.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<DailyTask> Tasks { get; set; }
    public DbSet<Habit> Habits { get; set; }
    public DbSet<HabitCompletion> HabitCompletions { get; set; }
    public DbSet<StudySession> StudySessions { get; set; }
    public DbSet<RoomParticipant> RoomParticipants { get; set; }
    public DbSet<XpTransaction> XpTransactions { get; set; }
    public DbSet<UserAchievement> UserAchievements { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<DailyTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<StudySession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .IsRequired();
            entity.HasIndex(e => e.UserId);

            entity.Property(e => e.ActiveUserId)
                  .HasColumnType("char(36)")
                  .IsRequired(false);
            entity.HasIndex(e => e.ActiveUserId)
                  .IsUnique();

            entity.Property(e => e.IsPaused)
                  .HasDefaultValue(false);
            entity.Property(e => e.AccumulatedSeconds)
                  .HasDefaultValue(0);
        });

        modelBuilder.Entity<RoomParticipant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RoomName)
                  .HasMaxLength(100)
                  .IsRequired();
            entity.Property(e => e.Username)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.HasIndex(e => new { e.RoomName, e.SeatNumber })
                  .IsUnique();
            entity.HasIndex(e => new { e.RoomName, e.UserId })
                  .IsUnique();
        });

        modelBuilder.Entity<HabitCompletion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Habit)
                  .WithMany(e => e.Completions)
                  .HasForeignKey(e => e.HabitId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.HabitId, e.Date })
                  .IsUnique();
        });

        modelBuilder.Entity<XpTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .IsRequired();

            entity.Property(e => e.SourceType)
                  .HasMaxLength(32)
                  .IsRequired();

            entity.Property(e => e.SourceId)
                  .HasMaxLength(64)
                  .IsRequired();

            entity.Property(e => e.Description)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.Amount)
                  .IsRequired();

            entity.Property(e => e.CreatedAt)
                  .IsRequired();

            entity.HasIndex(e => new { e.UserId, e.SourceType, e.SourceId })
                  .IsUnique();

            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
        });

        modelBuilder.Entity<UserAchievement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .IsRequired();

            entity.Property(e => e.AchievementCode)
                  .HasMaxLength(64)
                  .IsRequired();

            entity.Property(e => e.UnlockedAt)
                  .IsRequired();

            entity.Property(e => e.XpAwarded)
                  .IsRequired();

            entity.HasIndex(e => new { e.UserId, e.AchievementCode })
                  .IsUnique();

            entity.HasIndex(e => new { e.UserId, e.UnlockedAt });
        });
    }
}
