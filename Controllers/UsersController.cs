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
            var userDtos = users.Select(user => new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Roles = user.Roles,
                CreatedAt = user.CreatedAt,
                FavoriteReciters = user.FavoriteReciters,
                FirstName = user.FirstName,
                LastName = user.LastName
            }).ToList();
            return Ok(userDtos);
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
            
            var userDto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Roles = user.Roles,
                CreatedAt = user.CreatedAt,
                FavoriteReciters = user.FavoriteReciters,
                FirstName = user.FirstName,
                LastName = user.LastName
            };
            return Ok(userDto);
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
            var createdUser = await _mongoDbService.GetUserByEmailAsync(user.Email);
            
            if (createdUser == null)
            {
                return StatusCode(500, new { message = "Gebruiker aangemaakt maar niet gevonden" });
            }
            
            var userDto = new UserDto
            {
                Id = createdUser.Id,
                Username = createdUser.Username,
                Email = createdUser.Email,
                Roles = createdUser.Roles,
                CreatedAt = createdUser.CreatedAt,
                FavoriteReciters = createdUser.FavoriteReciters,
                FirstName = createdUser.FirstName,
                LastName = createdUser.LastName
            };
            return CreatedAtAction(nameof(GetUserById), new { id = createdUser.Id }, userDto);
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
            
            var userDto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Roles = user.Roles,
                CreatedAt = user.CreatedAt,
                FavoriteReciters = user.FavoriteReciters,
                FirstName = user.FirstName,
                LastName = user.LastName
            };
            return Ok(userDto);
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
            
            var userDto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Roles = user.Roles,
                CreatedAt = user.CreatedAt,
                FavoriteReciters = user.FavoriteReciters,
                FirstName = user.FirstName,
                LastName = user.LastName
            };
            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("me/favorite-reciters")]
    [Authorize]
    public async Task<IActionResult> GetFavoriteReciters()
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
            
            return Ok(new { favoriteReciters = user.FavoriteReciters });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting favorite reciters");
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPost("me/favorite-reciters/{reciterId}")]
    [Authorize]
    public async Task<IActionResult> AddFavoriteReciter(string reciterId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Unauthorized();
            }
            
            var user = await _mongoDbService.GetUserByIdAsync(userId);
            
            if (user == null)
            {
                return NotFound(new { message = "Gebruiker niet gevonden" });
            }
            
            if (!user.FavoriteReciters.Contains(reciterId))
            {
                user.FavoriteReciters.Add(reciterId);
                user.UpdatedAt = DateTime.UtcNow;
                await _mongoDbService.UpdateUserAsync(userId, user);
            }
            
            var userDto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Roles = user.Roles,
                CreatedAt = user.CreatedAt,
                FavoriteReciters = user.FavoriteReciters,
                FirstName = user.FirstName,
                LastName = user.LastName
            };
            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding favorite reciter {ReciterId}", reciterId);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpDelete("me/favorite-reciters/{reciterId}")]
    [Authorize]
    public async Task<IActionResult> RemoveFavoriteReciter(string reciterId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Unauthorized();
            }
            
            var user = await _mongoDbService.GetUserByIdAsync(userId);
            
            if (user == null)
            {
                return NotFound(new { message = "Gebruiker niet gevonden" });
            }
            
            if (user.FavoriteReciters.Contains(reciterId))
            {
                user.FavoriteReciters.Remove(reciterId);
                user.UpdatedAt = DateTime.UtcNow;
                await _mongoDbService.UpdateUserAsync(userId, user);
            }
            
            var userDto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Roles = user.Roles,
                CreatedAt = user.CreatedAt,
                FavoriteReciters = user.FavoriteReciters,
                FirstName = user.FirstName,
                LastName = user.LastName
            };
            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing favorite reciter {ReciterId}", reciterId);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPut("me/favorite-reciters")]
    [Authorize]
    public async Task<IActionResult> UpdateFavoriteReciters([FromBody] List<string> favoriteReciters)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Unauthorized();
            }
            
            var user = await _mongoDbService.GetUserByIdAsync(userId);
            
            if (user == null)
            {
                return NotFound(new { message = "Gebruiker niet gevonden" });
            }
            
            user.FavoriteReciters = favoriteReciters ?? new List<string>();
            user.UpdatedAt = DateTime.UtcNow;
            await _mongoDbService.UpdateUserAsync(userId, user);
            
            var userDto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Roles = user.Roles,
                CreatedAt = user.CreatedAt,
                FavoriteReciters = user.FavoriteReciters,
                FirstName = user.FirstName,
                LastName = user.LastName
            };
            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating favorite reciters");
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
} 