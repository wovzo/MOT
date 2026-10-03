using MediatR;
using MOT.Application.Common.Exceptions;
using MOT.Application.DTOs;
using MOT.Domain.Interfaces;

namespace MOT.Application.Handlers;

public class LoginUserHandler : IRequestHandler<LoginRequest, AuthResponse>
{
    private readonly IAuthRepository _authRepository;
    private readonly IJwtProvider _jwtProvider;
    private readonly IPasswordHasher _passwordHasher;

    public LoginUserHandler(IAuthRepository authRepository, IJwtProvider jwtProvider, IPasswordHasher passwordHasher)
    {
        _authRepository = authRepository;
        _jwtProvider = jwtProvider;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthResponse> Handle(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _authRepository.GetUserByEmailAsync(request.Email, cancellationToken);
        
        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }
        
        var token = _jwtProvider.GenerateToken(user);
        return new AuthResponse(token, user.Id, user.Email, user.DisplayName);
    }
}
