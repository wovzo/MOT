using MediatR;

namespace MOT.Application.DTOs;

public record LoginRequest(string Email, string Password) : IRequest<AuthResponse>;
