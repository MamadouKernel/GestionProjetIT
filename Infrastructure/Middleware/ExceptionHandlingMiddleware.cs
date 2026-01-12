using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace GestionProjects.Infrastructure.Middleware
{
    /// <summary>
    /// Middleware global de gestion des erreurs
    /// Capture toutes les exceptions non gérées et retourne une réponse appropriée
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
                _logger.LogError(ex, 
                    "Une erreur non gérée s'est produite. Path: {Path}, Method: {Method}, User: {User}",
                    context.Request.Path,
                    context.Request.Method,
                    context.User?.Identity?.Name ?? "Anonymous");

                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

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

            // Pour les erreurs spécifiques, ajuster le code de statut
            if (exception is UnauthorizedAccessException)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            }
            else if (exception is ArgumentException || exception is ArgumentNullException)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            else if (exception is KeyNotFoundException || exception is FileNotFoundException)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }
    }
}
