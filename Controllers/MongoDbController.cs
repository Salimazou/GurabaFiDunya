using Microsoft.AspNetCore.Mvc;
using server.Services;

namespace server.Controllers;

[ApiController]
[Route("api/mongodb")]
public class MongoDbController : ControllerBase
{
    private readonly MongoDbService _mongoDbService;
    private readonly ILogger<MongoDbController> _logger;

    public MongoDbController(MongoDbService mongoDbService, ILogger<MongoDbController> logger)
    {
        _mongoDbService = mongoDbService;
        _logger = logger;
    }

    [HttpGet("ping")]
    public async Task<IActionResult> Ping()
    {
        try
        {
            bool isConnected = await _mongoDbService.PingAsync();
            
            if (isConnected)
            {
                return Ok(new { status = "connected", message = "MongoDB connection successful" });
            }
            else
            {
                return StatusCode(500, new { status = "error", message = "Failed to connect to MongoDB" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pinging MongoDB");
            return StatusCode(500, new { status = "error", message = $"Error: {ex.Message}" });
        }
    }
} 