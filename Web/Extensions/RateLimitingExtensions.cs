using System.Threading.RateLimiting;

namespace GestionProjects.Web.Extensions;

public static class RateLimitingExtensions
{
    private static readonly string[] StaticPrefixes =
        ["/lib/", "/css/", "/js/", "/images/", "/fonts/", "/uploads/"];

    public static IServiceCollection AddCustomRateLimiting(
        this IServiceCollection services,
        IWebHostEnvironment environment)
    {
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (environment.IsDevelopment())
                    return RateLimitPartition.GetNoLimiter<string>("dev");

                var path = context.Request.Path.Value ?? string.Empty;
                var isStatic = StaticPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    || path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".map", StringComparison.OrdinalIgnoreCase);

                if (isStatic)
                    return RateLimitPartition.GetNoLimiter<string>("static");

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 500,
                        Window = TimeSpan.FromMinutes(1)
                    });
            });

            options.AddPolicy<string>("LoginPolicy", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(15)
                    }));

            options.AddPolicy<string>("UploadPolicy", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User?.Identity?.Name
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1)
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = 429;

                var isHtml = IsHtmlRequest(context.HttpContext.Request);
                if (!isHtml)
                {
                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsync(
                        System.Text.Json.JsonSerializer.Serialize(new
                        {
                            error = "Trop de requêtes. Veuillez réessayer plus tard.",
                            retryAfter = 60
                        }),
                        cancellationToken);
                }
            };
        });

        return services;
    }

    private static bool IsHtmlRequest(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Requested-With", out var xrw) &&
            string.Equals(xrw.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            return false;

        if (System.IO.Path.HasExtension(request.Path))
            return false;

        var fetchDest = request.Headers["Sec-Fetch-Dest"].ToString();
        if (!string.IsNullOrWhiteSpace(fetchDest))
            return fetchDest is "document" or "iframe";

        var accept = request.Headers.Accept.ToString();
        if (string.IsNullOrWhiteSpace(accept)) return true;
        if (accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)) return false;
        return accept.Contains("text/html", StringComparison.OrdinalIgnoreCase)
            || accept.Contains("*/*", StringComparison.OrdinalIgnoreCase);
    }
}
