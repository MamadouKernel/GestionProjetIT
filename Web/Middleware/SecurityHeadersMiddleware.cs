using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;

namespace GestionProjects.Web.Middleware
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var nonce = GenerateNonce();
            context.Items["CspNonce"] = nonce;

            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey("X-Content-Type-Options"))
                    context.Response.Headers["X-Content-Type-Options"] = "nosniff";

                if (!context.Response.Headers.ContainsKey("X-Frame-Options"))
                    context.Response.Headers["X-Frame-Options"] = "DENY";

                if (!context.Response.Headers.ContainsKey("X-XSS-Protection"))
                    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

                if (!context.Response.Headers.ContainsKey("Referrer-Policy"))
                    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

                if (!context.Response.Headers.ContainsKey("Permissions-Policy"))
                    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

                if (!context.Response.Headers.ContainsKey("Content-Security-Policy"))
                {
                    context.Response.Headers["Content-Security-Policy"] =
                        "default-src 'self'; " +
                        $"script-src 'self' 'nonce-{nonce}'; " +
                        // Styles inline autorisés : un nonce ne couvre pas les attributs style="" (Razor/SVG en
                        // utilisent), et le risque XSS via CSS est faible. Le nonce reste sur les scripts.
                        "style-src 'self' 'unsafe-inline'; " +
                        "img-src 'self' data:; " +
                        "font-src 'self'; " +
                        "connect-src 'self'; " +
                        "frame-ancestors 'none'; " +
                        "base-uri 'self'; " +
                        "form-action 'self';";
                }

                return Task.CompletedTask;
            });

            await _next(context);
        }

        private static string GenerateNonce()
        {
            var bytes = new byte[16];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }
    }
}
