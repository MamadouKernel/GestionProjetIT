using Microsoft.AspNetCore.Http;

namespace GestionProjects.Infrastructure.Middleware
{
    /// <summary>
    /// Middleware pour ajouter les headers de sécurité HTTP
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Ajouter les headers de sécurité
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "DENY");
            context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
            
            // Content Security Policy (CSP) - Restreinte selon les besoins réels
            // Scripts : uniquement locaux + inline (pour les scripts inline dans _Layout.cshtml)
            // Styles : locaux + inline + Bootstrap Icons CDN
            // Images : locales + data URIs (pour les icônes inline)
            // Fonts : locales + Bootstrap Icons CDN
            // Pas de unsafe-eval (sécurité renforcée)
            context.Response.Headers.Append("Content-Security-Policy", 
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline'; " + // Scripts locaux + inline (pas de unsafe-eval)
                "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " + // Styles locaux + inline + Bootstrap Icons
                "img-src 'self' data:; " + // Images locales + data URIs uniquement
                "font-src 'self' https://cdn.jsdelivr.net; " + // Fonts locales + Bootstrap Icons
                "connect-src 'self'; " + // AJAX uniquement vers le serveur
                "frame-ancestors 'none'; " + // Pas d'embedding
                "base-uri 'self'; " + // Base URI uniquement locale
                "form-action 'self';"); // Formulaires uniquement vers le serveur

            await _next(context);
        }
    }
}

