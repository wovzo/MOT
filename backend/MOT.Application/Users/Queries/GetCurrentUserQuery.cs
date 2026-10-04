using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
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

        public GetCurrentUserQueryHandler(IAuthRepository authRepository, ICurrentUserService currentUserService)
        {
            _authRepository = authRepository;
            _currentUserService = currentUserService;
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

            return new UserProfileDto(
                user.Id,
                user.Email,
                user.DisplayName,
                user.CreatedAt,
                user.CurrentStreak,
                user.Level,
                user.XP
            );
        }
    }
}
