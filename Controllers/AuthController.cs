using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using BCrypt.Net;
using GurabaFiDunya.Models;
using GurabaFiDunya.Services;
using GurabaFiDunya.DTOs;

namespace GurabaFiDunya.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly DatabaseService _databaseService;
    private readonly JwtService _jwtService;
    
    public AuthController(DatabaseService databaseService, JwtService jwtService)
    {
        _databaseService = databaseService;
        _jwtService = jwtService;
    }
    
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            // Validate input
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password) || string.IsNullOrEmpty(request.Name))
            {
                return BadRequest("Name, email, and password are required");
            }
            
            // Check if user already exists
            var existingUser = await _databaseService.Users
                .Find(u => u.Email == request.Email)
                .FirstOrDefaultAsync();
                
            if (existingUser != null)
            {
                return Conflict("User with this email already exists");
            }
            
            // Create new user
            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                StreakCount = 0,
                LastActiveDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            await _databaseService.Users.InsertOneAsync(user);
            
            // Generate JWT token
            var token = _jwtService.GenerateToken(user);
            
            var response = new AuthResponse
            {
                Token = token,
                UserId = user.Id,
                Name = user.Name,
                Email = user.Email,
                StreakCount = user.StreakCount,
                LastActiveDate = user.LastActiveDate
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            // Validate input
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest("Email and password are required");
            }
            
            // Find user
            var user = await _databaseService.Users
                .Find(u => u.Email == request.Email)
                .FirstOrDefaultAsync();
                
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized("Invalid email or password");
            }
            
            // Update last active date
            user.LastActiveDate = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            await _databaseService.Users.ReplaceOneAsync(u => u.Id == user.Id, user);
            
            // Generate JWT token
            var token = _jwtService.GenerateToken(user);
            
            var response = new AuthResponse
            {
                Token = token,
                UserId = user.Id,
                Name = user.Name,
                Email = user.Email,
                StreakCount = user.StreakCount,
                LastActiveDate = user.LastActiveDate
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid token");
            }
            
            var user = await _databaseService.Users
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync();
                
            if (user == null)
            {
                return NotFound("User not found");
            }
            
            var response = new UserProfileResponse
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                StreakCount = user.StreakCount,
                LastActiveDate = user.LastActiveDate,
                CreatedAt = user.CreatedAt
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] RegisterRequest request)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Invalid token");
            }
            
            var user = await _databaseService.Users
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync();
                
            if (user == null)
            {
                return NotFound("User not found");
            }
            
            // Update user fields
            if (!string.IsNullOrEmpty(request.Name))
            {
                user.Name = request.Name;
            }
            
            if (!string.IsNullOrEmpty(request.Password))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            }
            
            user.UpdatedAt = DateTime.UtcNow;
            
            await _databaseService.Users.ReplaceOneAsync(u => u.Id == userId, user);
            
            var response = new UserProfileResponse
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                StreakCount = user.StreakCount,
                LastActiveDate = user.LastActiveDate,
                CreatedAt = user.CreatedAt
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
} 