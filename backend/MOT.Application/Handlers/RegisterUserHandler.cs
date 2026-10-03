using MediatR;
using MOT.Application.Common.Exceptions;
using MOT.Application.DTOs;
using MOT.Domain.Entities;
using MOT.Domain.Interfaces;

namespace MOT.Application.Handlers;

public class RegisterUserHandler : IRequestHandler<RegisterRequest, AuthResponse>
{
    private readonly IAuthRepository _authRepository;
    private readonly IJwtProvider _jwtProvider;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterUserHandler(IAuthRepository authRepository, IJwtProvider jwtProvider, IPasswordHasher passwordHasher)
    {
        _authRepository = authRepository;
        _jwtProvider = jwtProvider;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthResponse> Handle(RegisterRequest request, CancellationToken cancellationToken)
    {
        var isUnique = await _authRepository.IsEmailUniqueAsync(request.Email, cancellationToken);
        if (!isUnique)
        {
            throw new DuplicateEmailException(request.Email);
        }

        var user = new User
        {
            Email = request.Email,
            DisplayName = request.DisplayName,
            PasswordHash = _passwordHasher.HashPassword(request.Password)
        };

        var createdUser = await _authRepository.CreateUserAsync(user, cancellationToken);
        var token = _jwtProvider.GenerateToken(createdUser);

        return new AuthResponse(token, createdUser.Id, createdUser.Email, createdUser.DisplayName);
    }
}
