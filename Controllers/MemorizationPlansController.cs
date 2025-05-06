using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using server.Models;
using server.Services;
using System.Security.Claims;

namespace server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MemorizationPlansController : ControllerBase
{
    private readonly MongoDbService _mongoDbService;
    
    public MemorizationPlansController(MongoDbService mongoDbService)
    {
        _mongoDbService = mongoDbService;
    }
    
    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException("User ID not found in token");
    }
    
    [HttpGet]
    public async Task<ActionResult<MemorizationPlan>> GetCurrentPlan()
    {
        var userId = GetUserId();
        var plan = await _mongoDbService.GetMemorizationPlanByUserIdAsync(userId);
        
        if (plan == null)
        {
            return NotFound("No active memorization plan found");
        }
        
        return Ok(plan);
    }
    
    [HttpPost]
    public async Task<ActionResult<MemorizationPlan>> CreatePlan(MemorizationPlan plan)
    {
        try
        {
            // Set the user ID from the authenticated user
            plan.UserId = GetUserId();
            
            await _mongoDbService.CreateMemorizationPlanAsync(plan);
            return CreatedAtAction(nameof(GetCurrentPlan), new { id = plan.Id }, plan);
        }
        catch (Exception ex)
        {
            return BadRequest($"Could not create memorization plan: {ex.Message}");
        }
    }
    
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePlan(string id, MemorizationPlan plan)
    {
        var userId = GetUserId();
        var existingPlan = await _mongoDbService.GetMemorizationPlanByIdAsync(id);
        
        if (existingPlan == null)
        {
            return NotFound($"Memorization plan with ID {id} not found");
        }
        
        if (existingPlan.UserId != userId)
        {
            return Forbid("You don't have permission to update this plan");
        }
        
        plan.Id = id;
        plan.UserId = userId; // Ensure user ID is not changed
        
        await _mongoDbService.UpdateMemorizationPlanAsync(id, plan);
        return NoContent();
    }
    
    [HttpPut("{id}/progress")]
    public async Task<IActionResult> UpdateProgress(string id, [FromBody] Progress progress)
    {
        var userId = GetUserId();
        var existingPlan = await _mongoDbService.GetMemorizationPlanByIdAsync(id);
        
        if (existingPlan == null)
        {
            return NotFound($"Memorization plan with ID {id} not found");
        }
        
        if (existingPlan.UserId != userId)
        {
            return Forbid("You don't have permission to update this plan");
        }
        
        await _mongoDbService.UpdateMemorizationPlanProgressAsync(id, progress);
        return NoContent();
    }
    
    [HttpPut("{id}/page-completed")]
    public async Task<IActionResult> MarkCurrentPageCompleted(string id)
    {
        var userId = GetUserId();
        var existingPlan = await _mongoDbService.GetMemorizationPlanByIdAsync(id);
        
        if (existingPlan == null)
        {
            return NotFound($"Memorization plan with ID {id} not found");
        }
        
        if (existingPlan.UserId != userId)
        {
            return Forbid("You don't have permission to update this plan");
        }
        
        // Update the current page as completed
        int currentPageIndex = existingPlan.CurrentPageIndex;
        if (currentPageIndex < existingPlan.PageBreakdown.Count)
        {
            // Mark current page as completed
            existingPlan.PageBreakdown[currentPageIndex].Completed = true;
            
            // Unlock next page if it exists
            if (currentPageIndex + 1 < existingPlan.PageBreakdown.Count)
            {
                existingPlan.PageBreakdown[currentPageIndex + 1].Unlocked = true;
            }
            
            // Add to memorized progress
            existingPlan.Progress.Memorized.Add(new ProgressItem {
                PageNumber = existingPlan.PageBreakdown[currentPageIndex].PageNumber,
                DateCompleted = DateTime.UtcNow
            });
            
            await _mongoDbService.UpdateMemorizationPlanAsync(id, existingPlan);
            return Ok(new { 
                success = true, 
                message = "Pagina gemarkeerd als gememoriseerd! Goed gedaan!" 
            });
        }
        
        return BadRequest(new { 
            success = false, 
            message = "Er is geen huidige pagina om te markeren als gememoriseerd." 
        });
    }
    
    [HttpPut("{id}/page-revised/{pageNumber}")]
    public async Task<IActionResult> MarkPageRevised(string id, int pageNumber)
    {
        var userId = GetUserId();
        var existingPlan = await _mongoDbService.GetMemorizationPlanByIdAsync(id);
        
        if (existingPlan == null)
        {
            return NotFound($"Memorization plan with ID {id} not found");
        }
        
        if (existingPlan.UserId != userId)
        {
            return Forbid("You don't have permission to update this plan");
        }
        
        // Find the page by page number
        var pageIndex = existingPlan.PageBreakdown.FindIndex(p => p.PageNumber == pageNumber);
        if (pageIndex != -1)
        {
            existingPlan.PageBreakdown[pageIndex].Revised = true;
            
            existingPlan.Progress.Revised.Add(new ProgressItem {
                PageNumber = pageNumber,
                DateCompleted = DateTime.UtcNow
            });
            
            await _mongoDbService.UpdateMemorizationPlanAsync(id, existingPlan);
            return Ok(new { 
                success = true, 
                message = "Pagina succesvol gemarkeerd als herhaald!" 
            });
        }
        
        return BadRequest(new { 
            success = false, 
            message = "Pagina niet gevonden om te markeren als herhaald." 
        });
    }
    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePlan(string id)
    {
        var userId = GetUserId();
        var existingPlan = await _mongoDbService.GetMemorizationPlanByIdAsync(id);
        
        if (existingPlan == null)
        {
            return NotFound($"Memorization plan with ID {id} not found");
        }
        
        if (existingPlan.UserId != userId)
        {
            return Forbid("You don't have permission to delete this plan");
        }
        
        await _mongoDbService.DeleteMemorizationPlanAsync(id);
        return NoContent();
    }
    
    [HttpDelete]
    public async Task<IActionResult> DeleteCurrentPlan()
    {
        var userId = GetUserId();
        await _mongoDbService.DeleteMemorizationPlansByUserIdAsync(userId);
        return NoContent();
    }
} 