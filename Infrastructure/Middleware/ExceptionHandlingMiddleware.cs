using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace GestionProjects.Infrastructure.Middleware
{
    /// <summary>
    /// Middleware global de gestion des erreurs.
    /// Pour les pages HTML, il laisse ensuite la page d'erreur MVC prendre le relais.
    /// Pour les appels techniques (AJAX / API), il renvoie une réponse JSON.
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Une erreur non gérée s'est produite. Path: {Path}, Method: {Method}, User: {User}",
                    context.Request.Path,
                    context.Request.Method,
                    context.User?.Identity?.Name ?? "Anonymous");

                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var statusCode = exception switch
            {
                UnauthorizedAccessException => (int)HttpStatusCode.Forbidden,
                ArgumentException => (int)HttpStatusCode.BadRequest,
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                FileNotFoundException => (int)HttpStatusCode.NotFound,
                _ => (int)HttpStatusCode.InternalServerError
            };

            if (ShouldRenderHtmlErrorPage(context.Request))
            {
                context.Items["ErrorStatusCode"] = statusCode;
                context.Items["ErrorOriginalPath"] = $"{context.Request.Path}{context.Request.QueryString}";
                context.Items["ErrorDetail"] = $"{exception.GetType().Name}: {exception.Message}";
                context.Response.Clear();
                context.Response.StatusCode = statusCode;
                return;
            }

            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.StatusCode = statusCode;

            var response = new
            {
                error = new
                {
                    message = _environment.IsDevelopment()
                        ? exception.Message
                        : "Une erreur s'est produite lors du traitement de votre demande.",
                    details = _environment.IsDevelopment() ? exception.ToString() : null,
                    requestId = context.TraceIdentifier
                }
            };

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }

        private static bool ShouldRenderHtmlErrorPage(HttpRequest request)
        {
            if (request.Headers.TryGetValue("X-Requested-With", out var requestedWith) &&
                string.Equals(requestedWith.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (Path.HasExtension(request.Path))
            {
                return false;
            }

            var fetchDest = request.Headers["Sec-Fetch-Dest"].ToString();
            if (!string.IsNullOrWhiteSpace(fetchDest))
            {
                return string.Equals(fetchDest, "document", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(fetchDest, "iframe", StringComparison.OrdinalIgnoreCase);
            }

            var accept = request.Headers.Accept.ToString();
            if (string.IsNullOrWhiteSpace(accept))
            {
                return true;
            }

            if (accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return accept.Contains("text/html", StringComparison.OrdinalIgnoreCase) ||
                   accept.Contains("*/*", StringComparison.OrdinalIgnoreCase);
        }
    }
}
