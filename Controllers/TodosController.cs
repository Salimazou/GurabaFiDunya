using Microsoft.AspNetCore.Mvc;
using server.Models;
using server.Services;

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
    
    [HttpGet]
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
    public async Task<IActionResult> GetTodosByUserId(string userId)
    {
        try
        {
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
    public async Task<IActionResult> GetTodoById(string id)
    {
        try
        {
            var todo = await _mongoDbService.GetTodoByIdAsync(id);
            
            if (todo == null)
            {
                return NotFound(new { message = "Todo niet gevonden" });
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
    public async Task<IActionResult> CreateTodo([FromBody] Todo todo)
    {
        try
        {
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
    public async Task<IActionResult> UpdateTodo(string id, [FromBody] Todo todo)
    {
        try
        {
            var existingTodo = await _mongoDbService.GetTodoByIdAsync(id);
            
            if (existingTodo == null)
            {
                return NotFound(new { message = "Todo niet gevonden" });
            }
            
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
    public async Task<IActionResult> MarkTodoAsComplete(string id)
    {
        try
        {
            var todo = await _mongoDbService.GetTodoByIdAsync(id);
            
            if (todo == null)
            {
                return NotFound(new { message = "Todo niet gevonden" });
            }
            
            await _mongoDbService.UpdateTodoCompletionAsync(id, true);
            
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
    public async Task<IActionResult> DeleteTodo(string id)
    {
        try
        {
            var todo = await _mongoDbService.GetTodoByIdAsync(id);
            
            if (todo == null)
            {
                return NotFound(new { message = "Todo niet gevonden" });
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