using Microsoft.AspNetCore.Http;

namespace BluePrintHr.Api.Middleware;

public sealed class CsrfOriginMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api")
            && MutatingMethods.Contains(context.Request.Method))
        {
            var fetchSite = context.Request.Headers["Sec-Fetch-Site"].FirstOrDefault();
            if (string.Equals(fetchSite, "cross-site", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "Cross-site request blocked." });
                return;
            }

            var origin = context.Request.Headers.Origin.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(origin))
            {
                var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                if (!allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { message = "Request origin is not trusted." });
                    return;
                }
            }
        }

        await next(context);
    }
}
