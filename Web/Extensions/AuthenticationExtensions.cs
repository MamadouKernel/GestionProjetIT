using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace GestionProjects.Web.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddCustomAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var authBuilder = services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath         = "/Account/Login";
                options.LogoutPath        = "/Account/Logout";
                options.AccessDeniedPath  = "/Home/Error/403";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan    = TimeSpan.FromMinutes(30);
                options.Cookie.Name       = ".GestionProjets.Auth";
                options.Cookie.HttpOnly   = true;
                // SameAsRequest : fonctionne en HTTP (intranet sans TLS) et HTTPS.
                // Passer à Always uniquement si un reverse proxy avec certificat TLS est en place.
                options.Cookie.SecurePolicy = environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.Cookie.SameSite     = SameSiteMode.Lax;
            });

        var oidcClientId = configuration["AzureAd:ClientId"]?.Trim();
        if (!string.IsNullOrWhiteSpace(oidcClientId))
        {
            authBuilder.AddOpenIdConnect("AzureAd", options =>
            {
                var instance             = configuration["AzureAd:Instance"]?.Trim();
                var tenantId             = configuration["AzureAd:TenantId"]?.Trim();
                var callbackPath         = configuration["AzureAd:CallbackPath"]?.Trim();
                var signedOutCallbackPath = configuration["AzureAd:SignedOutCallbackPath"]?.Trim();

                options.SignInScheme   = CookieAuthenticationDefaults.AuthenticationScheme;
                options.Authority      = $"{(string.IsNullOrWhiteSpace(instance) ? "https://login.microsoftonline.com/" : instance.TrimEnd('/') + "/")}{tenantId}/v2.0";
                options.ClientId       = oidcClientId;
                options.ClientSecret   = configuration["AzureAd:ClientSecret"]?.Trim() ?? string.Empty;
                options.CallbackPath   = string.IsNullOrWhiteSpace(callbackPath) ? "/signin-oidc" : callbackPath;
                options.SignedOutCallbackPath = string.IsNullOrWhiteSpace(signedOutCallbackPath)
                    ? "/signout-callback-oidc"
                    : signedOutCallbackPath;
                options.ResponseType                  = "code";
                options.SaveTokens                    = true;
                options.GetClaimsFromUserInfoEndpoint = false;
                options.RequireHttpsMetadata          = true;
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Events = new OpenIdConnectEvents
                {
                    OnRemoteFailure = context =>
                    {
                        context.HandleResponse();
                        context.Response.Redirect("/Account/Login");
                        return Task.CompletedTask;
                    }
                };
            });
        }

        services.AddHsts(options =>
        {
            options.IncludeSubDomains = true;
            options.MaxAge = TimeSpan.FromDays(365);
        });

        return services;
    }
}
