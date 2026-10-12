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
    }
}
