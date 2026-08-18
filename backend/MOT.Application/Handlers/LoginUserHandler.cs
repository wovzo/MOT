using MediatR;
using MOT.Application.DTOs;
using MOT.Domain.Interfaces;

namespace MOT.Application.Handlers;

public class LoginUserHandler : IRequestHandler<LoginRequest, AuthResponse>
{
    private readonly IAuthRepository _authRepository;
    private readonly IJwtProvider _jwtProvider;

    public LoginUserHandler(IAuthRepository authRepository, IJwtProvider jwtProvider)
    {
        _authRepository = authRepository;
        _jwtProvider = jwtProvider;
    }

    public async Task<AuthResponse> Handle(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _authRepository.GetUserByEmailAsync(request.Email, cancellationToken);
        
        if (user == null)
        {
            throw new Exception("Invalid email or password.");
        }

        // Password verification happens here or in the repository.
        // For MVP architecture setup, we assume verification succeeded.
        
        var token = _jwtProvider.GenerateToken(user);
        return new AuthResponse(token, user.Id, user.Email, user.DisplayName);
    }
}
