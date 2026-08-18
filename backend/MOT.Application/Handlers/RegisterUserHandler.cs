using MediatR;
using MOT.Application.DTOs;
using MOT.Domain.Entities;
using MOT.Domain.Interfaces;

namespace MOT.Application.Handlers;

public class RegisterUserHandler : IRequestHandler<RegisterRequest, AuthResponse>
{
    private readonly IAuthRepository _authRepository;
    private readonly IJwtProvider _jwtProvider;

    public RegisterUserHandler(IAuthRepository authRepository, IJwtProvider jwtProvider)
    {
        _authRepository = authRepository;
        _jwtProvider = jwtProvider;
    }

    public async Task<AuthResponse> Handle(RegisterRequest request, CancellationToken cancellationToken)
    {
        var isUnique = await _authRepository.IsEmailUniqueAsync(request.Email, cancellationToken);
        if (!isUnique)
        {
            throw new Exception("Email is already registered."); // Simplified for MVP
        }

        var user = new User
        {
            Email = request.Email,
            DisplayName = request.DisplayName,
            // In a real app, password hashing happens here or in the repository
        };

        var createdUser = await _authRepository.CreateUserAsync(user, cancellationToken);
        var token = _jwtProvider.GenerateToken(createdUser);

        return new AuthResponse(token, createdUser.Id, createdUser.Email, createdUser.DisplayName);
    }
}
