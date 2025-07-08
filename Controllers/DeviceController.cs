using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using server.Models;
using server.Services;
using System.Security.Claims;

namespace server.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize]
public class DeviceController : ControllerBase
{
    private readonly SimpleDbService _db;
    private readonly ILogger<DeviceController> _logger;

    public DeviceController(SimpleDbService db, ILogger<DeviceController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private string GetCurrentUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    [HttpPost("register-token")]
    public async Task<IActionResult> RegisterDeviceToken([FromBody] RegisterDeviceTokenRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            if (string.IsNullOrEmpty(request.DeviceToken))
            {
                return BadRequest(new { message = "Device token is required" });
            }

            await _db.UpdateUserDeviceTokenAsync(userId, request.DeviceToken);
            
            _logger.LogInformation($"Device token registered for user {userId} on platform {request.Platform}");
            
            return Ok(new { message = "Device token registered successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering device token");
            return StatusCode(500, new { message = "Server error" });
        }
    }

    [HttpDelete("unregister-token")]
    public async Task<IActionResult> UnregisterDeviceToken()
    {
        try
        {
            var userId = GetCurrentUserId();
            
            await _db.UpdateUserDeviceTokenAsync(userId, null);
            
            _logger.LogInformation($"Device token unregistered for user {userId}");
            
            return Ok(new { message = "Device token unregistered successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering device token");
            return StatusCode(500, new { message = "Server error" });
        }
    }
} 