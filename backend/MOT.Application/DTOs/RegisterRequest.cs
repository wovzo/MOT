using MediatR;

namespace MOT.Application.DTOs;

public record RegisterRequest(string Email, string Password, string DisplayName) : IRequest<AuthResponse>;
