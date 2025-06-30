using Microsoft.AspNetCore.Mvc;
using server.Models;
using server.Services;
using System.Security.Claims;

namespace server.Controllers;

[ApiController]
[Route("api/auth")]
public class SimpleAuthController : ControllerBase
{
    private readonly SimpleDbService _db;
    private readonly SimpleJwtService _jwt;
    private readonly ILogger<SimpleAuthController> _logger;

    public SimpleAuthController(SimpleDbService db, SimpleJwtService jwt, ILogger<SimpleAuthController> logger)
    {
        _db = db;
        _jwt = jwt;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var user = await _db.GetUserByEmailAsync(request.Email);
            
            if (user == null || !_db.VerifyPassword(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Onjuiste email of wachtwoord" });
            }

            var token = _jwt.GenerateToken(user);
            
            return Ok(new LoginResponse 
            { 
                Token = token, 
                User = user 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { message = "Server error" });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] User user)
    {
        try
        {
            var existing = await _db.GetUserByEmailAsync(user.Email);
            if (existing != null)
            {
                return Conflict(new { message = "Email al in gebruik" });
            }

            await _db.CreateUserAsync(user);
            
            // Re-fetch user to get the ID - with proper null checking
            var createdUser = await _db.GetUserByEmailAsync(user.Email);
            if (createdUser == null)
            {
                _logger.LogError("Failed to retrieve user after creation: {Email}", user.Email);
                return StatusCode(500, new { message = "Account aangemaakt maar er is een probleem opgetreden. Probeer in te loggen." });
            }

            var token = _jwt.GenerateToken(createdUser);

            return Ok(new LoginResponse 
            { 
                Token = token, 
                User = createdUser 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return StatusCode(500, new { message = "Server error" });
        }
    }

    // Helper method to get current user ID from token
    protected string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
    }
} 