using Microsoft.EntityFrameworkCore;
using MOT.Domain.Entities;

namespace MOT.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<DailyTask> Tasks { get; }
    DbSet<Habit> Habits { get; }
    DbSet<HabitCompletion> HabitCompletions { get; }
    DbSet<StudySession> StudySessions { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
