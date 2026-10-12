using System;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MOT.Application.Common.Exceptions;
using MOT.Application.DTOs;
using MOT.Application.Users.Queries;

namespace MOT.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableCors("AllowAll")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IMediator mediator, ILogger<AuthController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (FluentValidation.ValidationException ex)
            {
                var errors = ex.Errors.Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { Error = errors.FirstOrDefault() ?? "Validation failed.", Errors = errors });
            }
            catch (DuplicateEmailException ex)
            {
                return Conflict(new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during registration.");
                return StatusCode(500, new { Error = "An unexpected error occurred during registration." });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var response = await _mediator.Send(request);
                return Ok(response);
            }
            catch (FluentValidation.ValidationException ex)
            {
                var errors = ex.Errors.Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { Error = errors.FirstOrDefault() ?? "Validation failed.", Errors = errors });
            }
            catch (InvalidCredentialsException ex)
            {
                return Unauthorized(new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during login.");
                return StatusCode(500, new { Error = "An unexpected error occurred during login." });
            }
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var profile = await _mediator.Send(new GetCurrentUserQuery());
                return Ok(profile);
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while retrieving user profile.");
                return StatusCode(500, new { error = "An unexpected error occurred while retrieving user profile." });
            }
        }

#pragma warning disable CS0618
        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] MOT.Application.Users.Commands.ResetPasswordCommand command)
#pragma warning restore CS0618
        {
            // For security, unauthenticated password reset by email is disabled.
            // Returning a uniform error prevents account enumeration and prevents unauthorized password overrides.
            return BadRequest(new
            {
                Error = "Self-service password recovery is currently disabled. Please contact support or use authenticated password change."
            });
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] MOT.Application.Users.Commands.ChangePasswordCommand command)
        {
            try
            {
                await _mediator.Send(command);
                return Ok(new { Message = "Password changed successfully." });
            }
            catch (FluentValidation.ValidationException ex)
            {
                var errors = ex.Errors.Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { Error = errors.FirstOrDefault() ?? "Validation failed.", Errors = errors });
            }
            catch (InvalidCredentialsException ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while changing password.");
                return StatusCode(500, new { Error = "An unexpected error occurred while changing password." });
            }
        }

        [Authorize]
        [HttpPut("profile")]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateProfile([FromBody] MOT.Application.Users.Commands.UpdateProfileCommand command)
        {
            try
            {
                var profile = await _mediator.Send(command);
                return Ok(profile);
            }
            catch (FluentValidation.ValidationException ex)
            {
                var errors = ex.Errors.Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { Error = errors.FirstOrDefault() ?? "Validation failed.", Errors = errors });
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(new { Error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { Error = "User is not authenticated." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while updating user profile.");
                return StatusCode(500, new { Error = "An unexpected error occurred while updating user profile." });
            }
        }

        [Authorize]
        [HttpDelete("account")]
        [HttpDelete("me")]
        public async Task<IActionResult> DeleteAccount([FromBody] MOT.Application.Users.Commands.DeleteAccountCommand command)
        {
            try
            {
                await _mediator.Send(command);
                return Ok(new { Message = "Account deleted successfully." });
            }
            catch (FluentValidation.ValidationException ex)
            {
                var errors = ex.Errors.Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { Error = errors.FirstOrDefault() ?? "Validation failed.", Errors = errors });
            }
            catch (InvalidCredentialsException ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(new { Error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { Error = "User is not authenticated." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while deleting account.");
                return StatusCode(500, new { Error = "An unexpected error occurred while deleting account." });
            }
        }
    }
}

