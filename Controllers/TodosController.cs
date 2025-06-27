using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using server.Models;
using server.Services;
using System.Security.Claims;

namespace server.Controllers;

[ApiController]
[Route("api/todos")]
public class TodosController : ControllerBase
{
    private readonly ILogger<TodosController> _logger;
    private readonly MongoDbService _mongoDbService;

    public TodosController(ILogger<TodosController> logger, MongoDbService mongoDbService)
    {
        _logger = logger;
        _mongoDbService = mongoDbService;
    }
    
    private string GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("User ID claim not found in token");
        }
        return userId;
    }
    
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllTodos()
    {
        try
        {
            var todos = await _mongoDbService.GetAllTodosAsync();
            return Ok(todos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting todos");
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("user/{userId}")]
    [Authorize]
    public async Task<IActionResult> GetTodosByUserId(string userId)
    {
        try
        {
            // Only allow users to access their own todos or admins to access any todos
            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");
            
            if (currentUserId != userId && !isAdmin)
            {
                return Forbid();
            }
            
            var todos = await _mongoDbService.GetTodosByUserIdAsync(userId);
            return Ok(todos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting todos for user {UserId}", userId);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetTodoById(string id)
    {
        try
        {
            var todo = await _mongoDbService.GetTodoByIdAsync(id);
            
            if (todo == null)
            {
                return NotFound(new { message = "Todo niet gevonden" });
            }
            
            // Only allow users to access their own todos or admins to access any todos
            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");
            
            if (todo.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }
            
            return Ok(todo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting todo {TodoId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateTodo([FromBody] Todo todo)
    {
        try
        {
            // Set the user ID to the current user's ID unless admin
            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");
            
            if (!isAdmin && todo.UserId != currentUserId)
            {
                todo.UserId = currentUserId;
            }
            
            // Get user to set username
            var user = await _mongoDbService.GetUserByIdAsync(todo.UserId);
            if (user != null)
            {
                todo.Username = user.Username ?? user.Email;
            }
            
            // Make sure category is not null to avoid filtering issues
            if (string.IsNullOrWhiteSpace(todo.Category))
            {
                todo.Category = "General";
            }
            
            todo.CreatedAt = DateTime.UtcNow;
            await _mongoDbService.CreateTodoAsync(todo);
            
            // Re-fetch the todo to get the MongoDB ID
            var todos = await _mongoDbService.GetTodosByUserIdAsync(todo.UserId);
            var createdTodo = todos
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefault(t => t.Title == todo.Title);
                
            if (createdTodo == null)
            {
                return StatusCode(500, new { message = "Todo aangemaakt maar niet gevonden" });
            }
            
            return CreatedAtAction(nameof(GetTodoById), new { id = createdTodo.Id }, createdTodo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating todo");
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateTodo(string id, [FromBody] Todo todo)
    {
        try
        {
            var existingTodo = await _mongoDbService.GetTodoByIdAsync(id);
            
            if (existingTodo == null)
            {
                return NotFound(new { message = "Todo niet gevonden" });
            }
            
            // Only allow users to update their own todos or admins to update any todos
            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");
            
            if (existingTodo.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }
            
            // Preserve the original user ID and username
            todo.UserId = existingTodo.UserId;
            todo.Username = existingTodo.Username;
            
            todo.Id = id;
            todo.UpdatedAt = DateTime.UtcNow;
            todo.CreatedAt = existingTodo.CreatedAt; // Preserve original creation date
            
            await _mongoDbService.UpdateTodoAsync(id, todo);
            
            return Ok(todo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating todo {TodoId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpPatch("{id}/complete")]
    [Authorize]
    public async Task<IActionResult> MarkTodoAsComplete(string id)
    {
        try
        {
            var todo = await _mongoDbService.GetTodoByIdAsync(id);
            
            if (todo == null)
            {
                return NotFound(new { message = "Todo niet gevonden" });
            }
            
            // Only allow users to complete their own todos or admins to complete any todos
            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");
            
            if (todo.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }
            
            await _mongoDbService.UpdateTodoCompletionAsync(id, !todo.IsCompleted);
            
            // Re-fetch the updated todo
            todo = await _mongoDbService.GetTodoByIdAsync(id);
            
            return Ok(todo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking todo {TodoId} as complete", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
    
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteTodo(string id)
    {
        try
        {
            var todo = await _mongoDbService.GetTodoByIdAsync(id);
            
            if (todo == null)
            {
                return NotFound(new { message = "Todo niet gevonden" });
            }
            
            // Only allow users to delete their own todos or admins to delete any todos
            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");
            
            if (todo.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }
            
            await _mongoDbService.DeleteTodoAsync(id);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting todo {TodoId}", id);
            return StatusCode(500, new { message = "Een interne serverfout is opgetreden" });
        }
    }
} 