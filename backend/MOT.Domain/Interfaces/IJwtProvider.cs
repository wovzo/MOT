using MOT.Domain.Entities;

namespace MOT.Domain.Interfaces;

public interface IJwtProvider
{
    string GenerateToken(User user);
}
