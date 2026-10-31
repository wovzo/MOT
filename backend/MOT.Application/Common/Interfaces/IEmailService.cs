using System.Threading;
using System.Threading.Tasks;

namespace MOT.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetOtpAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default);
}
