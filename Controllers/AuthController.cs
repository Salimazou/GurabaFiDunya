using Microsoft.AspNetCore.Mvc;
using server.Models;
using server.Services;
using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;

namespace server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly MongoDbService _mongoDbService;
    private readonly JwtService _jwtService;

    public AuthController(
        ILogger<AuthController> logger, 
        MongoDbService mongoDbService,
        JwtService jwtService)
    {
        _logger = logger;
        _mongoDbService = mongoDbService;
        _jwtService = jwtService;
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        try
        {
            _logger.LogInformation("Login attempt: {Email}", loginDto.Email);
            
            // Find user by email
            var user = await _mongoDbService.GetUserByEmailAsync(loginDto.Email);
            
            if (user == null)
            {
                return Unauthorized(new { message = "Email niet gevonden" });
            }
            
            // For demo, use simple password check
            // In production, use proper password hashing
            if (user.PasswordHash != ComputePasswordHash(loginDto.Password))
            {
                return Unauthorized(new { message = "Onjuist wachtwoord" });
            }
            
            // Generate JWT token
            var token = _jwtService.GenerateToken(user);
            
            // Create response
            var response = new AuthResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Roles = user.Roles,
                Token = token
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
    {
        try
        {
            _logger.LogInformation("Registration attempt: {Email}", registerDto.Email);
            
            // Check if email already exists
            var existingUser = await _mongoDbService.GetUserByEmailAsync(registerDto.Email);
            if (existingUser != null)
            {
                return Conflict(new { message = "Deze email is al in gebruik" });
            }
            
            // Create new user with User role by default
            var user = new User
            {
                Username = registerDto.Username,
                Email = registerDto.Email,
                PasswordHash = ComputePasswordHash(registerDto.Password),
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                Roles = new List<string> { "User" },
                CreatedAt = DateTime.UtcNow
            };
            
            await _mongoDbService.CreateUserAsync(user);
            
            // Re-fetch the user to get the ID assigned by MongoDB
            user = await _mongoDbService.GetUserByEmailAsync(registerDto.Email);
            
            // Generate JWT token
            var token = _jwtService.GenerateToken(user);
            
            var response = new AuthResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Roles = user.Roles,
                Token = token
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "Niet geauthenticeerd" });
            }
            
            var user = await _mongoDbService.GetUserByIdAsync(userId);
            
            if (user == null)
            {
                return NotFound(new { message = "Gebruiker niet gevonden" });
            }
            
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    private string ComputePasswordHash(string password)
    {
        // Simple hashing for demonstration
        // In a real app, use a proper password hashing library
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }
} 