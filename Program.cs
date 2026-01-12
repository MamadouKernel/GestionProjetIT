using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Middleware;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

// Configuration Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/gestion-projets-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Utiliser Serilog comme logger
builder.Host.UseSerilog();

// DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.CommandTimeout(30) // Timeout de 30 secondes pour les commandes SQL
    );
});

// CurrentUserService
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Cache
builder.Services.AddMemoryCache();
builder.Services.AddScoped<GestionProjects.Application.Common.Interfaces.ICacheService, GestionProjects.Infrastructure.Services.CacheService>();

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Rate limiting global : 100 requêtes par minute par IP
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Rate limiting pour le login : 5 tentatives par 15 minutes par IP
    options.AddPolicy<string>("LoginPolicy", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15)
            }));

    // Rate limiting pour les uploads : 20 uploads par minute par utilisateur/IP
    options.AddPolicy<string>("UploadPolicy", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Gestion des réponses en cas de limite atteinte
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = 429; // Too Many Requests
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                error = "Trop de requêtes. Veuillez réessayer plus tard.",
                retryAfter = 60
            }),
            cancellationToken);
    };
});

// Services
builder.Services.AddScoped<GestionProjects.Infrastructure.Services.IFileStorageService, GestionProjects.Infrastructure.Services.FileStorageService>();
builder.Services.AddScoped<GestionProjects.Infrastructure.Services.IAuditService, GestionProjects.Infrastructure.Services.AuditService>();
builder.Services.AddScoped<GestionProjects.Infrastructure.Services.IPdfService, GestionProjects.Infrastructure.Services.PdfService>();
builder.Services.AddScoped<GestionProjects.Infrastructure.Services.IExcelService, GestionProjects.Infrastructure.Services.ExcelService>();
builder.Services.AddScoped<GestionProjects.Infrastructure.Services.IWordService, GestionProjects.Infrastructure.Services.WordService>();
builder.Services.AddScoped<GestionProjects.Application.Common.Interfaces.INotificationService, GestionProjects.Infrastructure.Services.NotificationService>();
builder.Services.AddScoped<GestionProjects.Application.Common.Interfaces.ILivrableValidationService, GestionProjects.Infrastructure.Services.LivrableValidationService>();
builder.Services.AddScoped<GestionProjects.Application.Common.Interfaces.IRAGCalculationService, GestionProjects.Infrastructure.Services.RAGCalculationService>();

// Authentification par cookies
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";        // page de login
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;            // Renouvelle le timeout à chaque requête
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // Déconnexion après 30 min d'inactivité
        options.Cookie.HttpOnly = true;
        // Sécurité renforcée : Always en production, SameAsRequest en développement
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
            ? CookieSecurePolicy.SameAsRequest 
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = builder.Environment.IsDevelopment() 
            ? SameSiteMode.Lax 
            : SameSiteMode.Strict;
    });

// Autorisation : par d�faut TOUT n�cessite d��tre authentifi�
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.Filters.Add(new AuthorizeFilter(policy));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "⚠️ Erreur lors de la migration");
    }

    if (!db.Utilisateurs.Any())
    {
        try
        {
            // Générer un mot de passe aléatoire sécurisé
            var randomPassword = GenerateSecurePassword();
            var hash = BCrypt.Net.BCrypt.HashPassword(randomPassword);

            var adminId = Guid.NewGuid();
            var admin = new Utilisateur
            {
                Id = adminId,
                Matricule = "admin",
                MotDePasse = hash,
                Nom = "Administrateur",
                Prenoms = "DSI",
                Email = "admin@cit.ci",
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM",
                ModifiePar = string.Empty, // Temporaire : sera null après migration
                EstSupprime = false,
                NombreConnexion = 0
            };

            db.Utilisateurs.Add(admin);
            
            // Ajouter le rôle AdminIT
            db.UtilisateurRoles.Add(new UtilisateurRole
            {
                Id = Guid.NewGuid(),
                UtilisateurId = adminId,
                Role = RoleUtilisateur.AdminIT,
                DateDebut = DateTime.Now,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM",
                EstSupprime = false
            });
            
            db.SaveChanges();
            // Ne logger QUE le matricule, jamais le mot de passe
            Log.Information("Utilisateur admin créé avec succès. Matricule: admin. Le mot de passe a été généré aléatoirement et doit être réinitialisé au premier login.");
            // En développement uniquement, sauvegarder le mot de passe dans un fichier sécurisé
            if (app.Environment.IsDevelopment())
            {
                var passwordFile = Path.Combine(app.Environment.ContentRootPath, "admin-password.txt");
                System.IO.File.WriteAllText(passwordFile, $"Mot de passe admin temporaire: {randomPassword}\nATTENTION: Supprimer ce fichier après utilisation!");
                Log.Warning("⚠️ Mot de passe admin sauvegardé dans admin-password.txt - À SUPPRIMER après utilisation!");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur lors de la création de l'utilisateur admin");
        }
    }
    else
    {
        var adminExists = db.Utilisateurs.FirstOrDefault(u => u.Matricule == "admin");
        if (adminExists != null)
        {
            Log.Information("✅ Utilisateur admin trouvé. Nombre d'utilisateurs: {Count}", db.Utilisateurs.Count());
        }
        else
        {
            Log.Warning("⚠️ Aucun utilisateur admin trouvé. Création...");
            try
            {
                // Générer un mot de passe aléatoire sécurisé
                var randomPassword = GenerateSecurePassword();
                var hash = BCrypt.Net.BCrypt.HashPassword(randomPassword);
                var adminId = Guid.NewGuid();
                var admin = new Utilisateur
                {
                    Id = adminId,
                    Matricule = "admin",
                    MotDePasse = hash,
                    Nom = "Administrateur",
                    Prenoms = "DSI",
                    Email = "admin@cit.ci",
                    DateCreation = DateTime.Now,
                    CreePar = "SYSTEM",
                    ModifiePar = string.Empty,
                    EstSupprime = false,
                    NombreConnexion = 0
                };
                db.Utilisateurs.Add(admin);
                
                // Ajouter le rôle AdminIT
                db.UtilisateurRoles.Add(new UtilisateurRole
                {
                    Id = Guid.NewGuid(),
                    UtilisateurId = adminId,
                    Role = RoleUtilisateur.AdminIT,
                    DateDebut = DateTime.Now,
                    DateCreation = DateTime.Now,
                    CreePar = "SYSTEM",
                    EstSupprime = false
                });
                
                db.SaveChanges();
                Log.Information("✅ Utilisateur admin créé. Le mot de passe a été généré aléatoirement.");
                // En développement uniquement
                if (app.Environment.IsDevelopment())
                {
                    var passwordFile = Path.Combine(app.Environment.ContentRootPath, "admin-password.txt");
                    System.IO.File.WriteAllText(passwordFile, $"Mot de passe admin temporaire: {randomPassword}\nATTENTION: Supprimer ce fichier après utilisation!");
                    Log.Warning("⚠️ Mot de passe admin sauvegardé dans admin-password.txt - À SUPPRIMER après utilisation!");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Erreur lors de la création de l'utilisateur admin");
            }
        }
    }
}


// Middleware de gestion d'erreurs global (doit être avant les autres middlewares)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Middleware pour les headers de sécurité HTTP
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Localisation
var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(localizationOptions);

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Rate Limiting (doit être après UseRouting)
app.UseRateLimiter();

// Auth + Authorize
app.UseAuthentication();
app.UseAuthorization();

// Route par d�faut : va sur Login si pas connect�
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();

// Fonction helper pour générer un mot de passe sécurisé
static string GenerateSecurePassword()
{
    const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    const string lowercase = "abcdefghijklmnopqrstuvwxyz";
    const string digits = "0123456789";
    const string special = "@#$%&*!";
    const string allChars = uppercase + lowercase + digits + special;
    
    var random = new Random();
    var password = new System.Text.StringBuilder();
    
    // Garantir au moins un caractère de chaque type
    password.Append(uppercase[random.Next(uppercase.Length)]);
    password.Append(lowercase[random.Next(lowercase.Length)]);
    password.Append(digits[random.Next(digits.Length)]);
    password.Append(special[random.Next(special.Length)]);
    
    // Ajouter 8 caractères aléatoires supplémentaires (total 12)
    for (int i = 0; i < 8; i++)
    {
        password.Append(allChars[random.Next(allChars.Length)]);
    }
    
    // Mélanger les caractères
    var chars = password.ToString().ToCharArray();
    for (int i = chars.Length - 1; i > 0; i--)
    {
        int j = random.Next(i + 1);
        (chars[i], chars[j]) = (chars[j], chars[i]);
    }
    
    return new string(chars);
}
