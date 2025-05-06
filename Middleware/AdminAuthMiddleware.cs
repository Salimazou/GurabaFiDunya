using System.Security.Claims;
using server.Services;

namespace server.Middleware;

public class AdminAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly JwtService _jwtService;
    
    public AdminAuthMiddleware(RequestDelegate next, JwtService jwtService)
    {
        _next = next;
        _jwtService = jwtService;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;
        
        // Skip admin check for non-admin routes
        if (!IsAdminProtectedEndpoint(path, method))
        {
            await _next(context);
            return;
        }
        
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader == null || !authHeader.StartsWith("Bearer "))
        {
            context.Response.StatusCode = 401; // Unauthorized
            await context.Response.WriteAsJsonAsync(new { message = "Authentication required" });
            return;
        }
        
        var token = authHeader.Substring("Bearer ".Length);
        var principal = _jwtService.ValidateToken(token);
        
        if (principal == null)
        {
            context.Response.StatusCode = 401; // Unauthorized
            await context.Response.WriteAsJsonAsync(new { message = "Invalid or expired token" });
            return;
        }
        
        // Check if user has admin role
        var isAdmin = principal.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Any(c => c.Value == "Admin");
            
        if (!isAdmin)
        {
            context.Response.StatusCode = 403; // Forbidden
            await context.Response.WriteAsJsonAsync(new { message = "Admin privileges required" });
            return;
        }
        
        // Set user identity for the request
        context.User = principal;
        await _next(context);
    }
    
    private bool IsAdminProtectedEndpoint(string path, string method)
    {
        // These paths are always admin-only
        if (path.StartsWith("/api/admin") || 
            path.StartsWith("/api/users") ||
            path.StartsWith("/api/stats"))
        {
            return true;
        }
        
        // GET /api/todos (all todos) is admin-only, but not /api/todos/user/{userId}
        if (path.Equals("/api/todos") && method.Equals("GET"))
        {
            return true;
        }
        
        return false;
    }
} 