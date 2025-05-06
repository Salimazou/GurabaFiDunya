namespace server.Middleware;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseAdminAuth(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AdminAuthMiddleware>();
    }
} 