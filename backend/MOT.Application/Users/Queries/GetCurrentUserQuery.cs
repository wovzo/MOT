using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.DTOs;
using MOT.Domain.Interfaces;

namespace MOT.Application.Users.Queries
{
    public class GetCurrentUserQuery : IRequest<UserProfileDto>
    {
    }

    public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserProfileDto>
    {
        private readonly IAuthRepository _authRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAppDbContext _context;

        public GetCurrentUserQueryHandler(
            IAuthRepository authRepository,
            ICurrentUserService currentUserService,
            IAppDbContext context)
        {
            _authRepository = authRepository;
            _currentUserService = currentUserService;
            _context = context;
        }

        public async Task<UserProfileDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
        {
            var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedAccessException();

            var user = await _authRepository.GetUserByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                throw new UserNotFoundException(userId);
            }

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
}
