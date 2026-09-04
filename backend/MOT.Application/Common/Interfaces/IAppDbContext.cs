using Microsoft.EntityFrameworkCore;
using MOT.Domain.Entities;

namespace MOT.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<DailyTask> Tasks { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
