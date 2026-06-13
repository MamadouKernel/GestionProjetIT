using GestionProjects.Application.Common.Extensions;
using GestionProjects.Infrastructure.Extensions;
using GestionProjects.Infrastructure.Middleware;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Serilog;

// ── Logging ──────────────────────────────────────────────────────────────────
var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "gestion-projets-.txt");
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ── Services ─────────────────────────────────────────────────────────────────
builder.Services
    .AddDatabase(builder.Configuration)
    .AddApplicationServices()
    .AddCustomAuthentication(builder.Configuration, builder.Environment)
    .AddCustomRateLimiting(builder.Environment)
    .AddCustomHealthChecks()
    .AddControllersWithGlobalFilters();

// ── Localisation (French) ─────────────────────────────────────────────────────
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var fr = new System.Globalization.CultureInfo("fr-FR");
    options.DefaultRequestCulture = new RequestCulture(fr);
    options.SupportedCultures     = [fr];
    options.SupportedUICultures   = [fr];
});

// ── App pipeline ──────────────────────────────────────────────────────────────
var app = builder.Build();

if (ShouldApplyDatabaseStartupTasks(app))
{
    await app.Services.ApplyMigrationsAsync();
}
else
{
    Log.Information(
        "Taches BDD de demarrage desactivees pour l'environnement {Environment}. Activez Database:ApplyMigrationsOnStartup explicitement si necessaire.",
        app.Environment.EnvironmentName);
}

if (app.Configuration.GetValue<bool>("SeedDemo:Enabled"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try { await SeedDonneesDemo.ExecuterAsync(db, app.Configuration); }
    catch (Exception ex) { Log.Warning(ex, "Seed démonstration ignoré"); }
}

app.UseWhen(
    ctx => ShouldRenderHtmlPage(ctx.Request),
    pipeline => pipeline.UseStatusCodePagesWithReExecute("/Home/Error/{0}"));

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseRequestLocalization(
    app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

// Protéger l'accès aux fichiers uploadés (auth requise)
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads"))
    {
        var auth = await context.AuthenticateAsync(
            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
        if (!auth.Succeeded)
        {
            context.Response.Redirect(
                $"/Account/Login?returnUrl={Uri.EscapeDataString(context.Request.Path)}");
            return;
        }
    }
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthCheckEndpoints();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();

// ── Helpers ───────────────────────────────────────────────────────────────────
static bool ShouldRenderHtmlPage(HttpRequest request)
{
    if (request.Headers.TryGetValue("X-Requested-With", out var xrw) &&
        string.Equals(xrw.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        return false;

    if (Path.HasExtension(request.Path)) return false;

    var fetchDest = request.Headers["Sec-Fetch-Dest"].ToString();
    if (!string.IsNullOrWhiteSpace(fetchDest))
        return fetchDest is "document" or "iframe";

    var accept = request.Headers.Accept.ToString();
    if (string.IsNullOrWhiteSpace(accept)) return true;
    if (accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)) return false;
    return accept.Contains("text/html", StringComparison.OrdinalIgnoreCase)
        || accept.Contains("*/*", StringComparison.OrdinalIgnoreCase);
}

static bool ShouldApplyDatabaseStartupTasks(WebApplication app) =>
    app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");

public partial class Program { }
