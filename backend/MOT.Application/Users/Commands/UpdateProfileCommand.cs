using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.DTOs;

namespace MOT.Application.Users.Commands;

public class UpdateProfileCommand : IRequest<UserProfileDto>
{
    public string DisplayName { get; set; } = string.Empty;
}

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, UserProfileDto>
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateProfileCommandHandler(
        IAppDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<UserProfileDto> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        // Only update permitted profile fields; protect all other properties
        user.DisplayName = request.DisplayName.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        var totalFocusMinutes = await _context.StudySessions
            .Where(s => s.UserId == userId && s.EndTime != null)
            .SumAsync(s => s.DurationMinutes, cancellationToken);

        return new UserProfileDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.CreatedAt,
            user.CurrentStreak,
            user.Level,
            user.XP,
            totalFocusMinutes
        );
    }
}
