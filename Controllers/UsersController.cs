using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using server.Models;
using server.Services;
using System.Security.Claims;

namespace server.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly ILogger<UsersController> _logger;
    private readonly MongoDbService _mongoDbService;

    public UsersController(ILogger<UsersController> logger, MongoDbService mongoDbService)
    {
        _logger = logger;
        _mongoDbService = mongoDbService;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            var users = await _mongoDbService.GetAllUsersAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(string id)
    {
        try
        {
            var user = await _mongoDbService.GetUserByIdAsync(id);
            
            if (user == null)
            {
                return NotFound(new { message = "Gebruiker niet gevonden" });
            }
            
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] User user)
    {
        try
        {
            // Check if email already exists
            var existingUser = await _mongoDbService.GetUserByEmailAsync(user.Email);
            if (existingUser != null)
            {
                return Conflict(new { message = "Deze email is al in gebruik" });
            }
            
            user.CreatedAt = DateTime.UtcNow;
            await _mongoDbService.CreateUserAsync(user);
            
            // Re-fetch the user to get the ID assigned by MongoDB
            user = await _mongoDbService.GetUserByEmailAsync(user.Email);
            
            return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] User user)
    {
        try
        {
            var existingUser = await _mongoDbService.GetUserByIdAsync(id);
            
            if (existingUser == null)
            {
                return NotFound(new { message = "Gebruiker niet gevonden" });
            }
            
            // Check if email is changed and already exists
            if (user.Email != existingUser.Email)
            {
                var userWithSameEmail = await _mongoDbService.GetUserByEmailAsync(user.Email);
                if (userWithSameEmail != null)
                {
                    return Conflict(new { message = "Deze email is al in gebruik" });
                }
            }
            
            user.Id = id;
            user.CreatedAt = existingUser.CreatedAt;
            user.UpdatedAt = DateTime.UtcNow;
            
            await _mongoDbService.UpdateUserAsync(id, user);
            
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        try
        {
            var user = await _mongoDbService.GetUserByIdAsync(id);
            
            if (user == null)
            {
                return NotFound(new { message = "Gebruiker niet gevonden" });
            }
            
            await _mongoDbService.DeleteUserAsync(id);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("me")]
    [Authorize]
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
} 