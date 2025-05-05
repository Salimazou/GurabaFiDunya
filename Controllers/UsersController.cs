using Microsoft.AspNetCore.Mvc;
using server.Models;

namespace server.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly ILogger<UsersController> _logger;

    public UsersController(ILogger<UsersController> logger)
    {
        _logger = logger;
    }
    
    [HttpGet]
    public IActionResult GetAllUsers()
    {
        try
        {
            // Mock data for demo purposes
            // In a real app, you would fetch from a database
            var users = new List<User>
            {
                new User
                {
                    Id = "1",
                    Username = "parent1",
                    Email = "parent1@example.com",
                    FirstName = "Ahmed",
                    LastName = "Ali",
                    Roles = new List<string> { "Parent" }
                },
                new User
                {
                    Id = "2",
                    Username = "parent2",
                    Email = "parent2@example.com",
                    FirstName = "Fatima",
                    LastName = "Ali",
                    Roles = new List<string> { "Parent" }
                },
                new User
                {
                    Id = "3",
                    Username = "child1",
                    Email = "child1@example.com",
                    FirstName = "Mohammed",
                    LastName = "Ali",
                    Roles = new List<string> { "Child" }
                }
            };
            
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("{id}")]
    public IActionResult GetUserById(string id)
    {
        try
        {
            // Mock data for demo purposes
            // In a real app, you would fetch from a database based on id
            var user = new User
            {
                Id = id,
                Username = "parent1",
                Email = "parent1@example.com",
                FirstName = "Ahmed",
                LastName = "Ali",
                Roles = new List<string> { "Parent" }
            };
            
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPost]
    public IActionResult CreateUser([FromBody] User user)
    {
        try
        {
            // This would normally create a new user in your database
            // For this demo, we'll just return success with the user with an ID
            user.Id = Guid.NewGuid().ToString();
            
            return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPut("{id}")]
    public IActionResult UpdateUser(string id, [FromBody] User user)
    {
        try
        {
            // This would normally update a user in your database
            // For this demo, we'll just return success
            user.Id = id;
            user.UpdatedAt = DateTime.UtcNow;
            
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpDelete("{id}")]
    public IActionResult DeleteUser(string id)
    {
        try
        {
            // This would normally delete a user from your database
            // For this demo, we'll just return success
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
} 