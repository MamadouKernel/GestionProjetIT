using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Middleware;
using GestionProjects.Infrastructure.Persistence;
using GestionProjects.Infrastructure.Security;
using GestionProjects.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
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
    // Rate limiting global : désactivé en développement, actif uniquement en production
    // sur les requêtes dynamiques (les fichiers statiques sont toujours exclus)
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // En développement : aucune limite
        if (builder.Environment.IsDevelopment())
            return System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter<string>("dev");

        // Exclure les fichiers statiques du rate limiter global
        var path = context.Request.Path.Value ?? string.Empty;
        var isStaticFile = path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/fonts/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".map", StringComparison.OrdinalIgnoreCase);

        if (isStaticFile)
            return System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter<string>("static");

        // Production : 500 requêtes dynamiques par minute par IP
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 500,
                Window = TimeSpan.FromMinutes(1)
            });
    });

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
        var request = context.HttpContext.Request;
        var isHtmlRequest = ShouldRenderHtmlErrorPage(request);

        context.HttpContext.Response.StatusCode = 429; // Too Many Requests

        if (isHtmlRequest)
        {
            return;
        }

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
builder.Services.AddScoped<GestionProjects.Infrastructure.Services.IDocumentPreviewService, GestionProjects.Infrastructure.Services.DocumentPreviewService>();
builder.Services.AddScoped<GestionProjects.Infrastructure.Services.IAuditService, GestionProjects.Infrastructure.Services.AuditService>();
builder.Services.AddScoped<GestionProjects.Infrastructure.Services.IPdfService, GestionProjects.Infrastructure.Services.PdfService>();
builder.Services.AddScoped<GestionProjects.Infrastructure.Services.IExcelService, GestionProjects.Infrastructure.Services.ExcelService>();
builder.Services.AddScoped<GestionProjects.Infrastructure.Services.IWordService, GestionProjects.Infrastructure.Services.WordService>();
builder.Services.AddScoped<GestionProjects.Application.Common.Interfaces.INotificationService, GestionProjects.Infrastructure.Services.NotificationService>();
builder.Services.AddScoped<GestionProjects.Application.Common.Interfaces.ILivrableValidationService, GestionProjects.Infrastructure.Services.LivrableValidationService>();
builder.Services.AddScoped<GestionProjects.Application.Common.Interfaces.IRAGCalculationService, GestionProjects.Infrastructure.Services.RAGCalculationService>();
builder.Services.AddScoped<GestionProjects.Application.Common.Interfaces.ITeamsNotificationService, GestionProjects.Infrastructure.Services.TeamsNotificationService>();
builder.Services.AddHttpClient("Teams")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<GestionProjects.Application.Common.Interfaces.IEmailService, GestionProjects.Infrastructure.Services.EmailService>();
builder.Services.AddScoped<GestionProjects.Application.Common.Interfaces.IUtilisateurService, GestionProjects.Infrastructure.Services.UtilisateurService>();
builder.Services.AddScoped<GestionProjects.Application.Common.Interfaces.IProjetQueryService, GestionProjects.Infrastructure.Services.ProjetQueryService>();
builder.Services.AddScoped<GestionProjects.Application.Common.Interfaces.IUatValidationService, GestionProjects.Infrastructure.Services.UatValidationService>();
builder.Services.AddScoped<GestionProjects.Application.Common.Interfaces.ICollaborationProjetService, GestionProjects.Infrastructure.Services.CollaborationProjetService>();
builder.Services.AddScoped<GestionProjects.Application.Common.Interfaces.IElectronicSignatureService, GestionProjects.Infrastructure.Services.ElectronicSignatureService>();
builder.Services.AddScoped<GestionProjects.Application.Common.Interfaces.IPermissionService, GestionProjects.Infrastructure.Services.PermissionService>();
builder.Services.AddScoped<PermissionMatrixAuthorizationFilter>();

// ── Azure AD (désactivé en dev — décommenter et configurer pour la prod) ──────
// builder.Services
//     .AddAuthentication(options =>
//     {
//         options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
//         options.DefaultChallengeScheme = "AzureAd";
//     })
//     .AddCookie()
//     .AddOpenIdConnect("AzureAd", options =>
//     {
//         builder.Configuration.GetSection("AzureAd").Bind(options);
//         options.ResponseType = "code";
//         options.SaveTokens   = true;
//         options.Scope.Add("openid");
//         options.Scope.Add("profile");
//         options.Scope.Add("email");
//     });

// Authentification par cookies
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";        // page de login
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Home/Error/403";
        options.SlidingExpiration = true;            // Renouvelle le timeout à chaque requête
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // Déconnexion après 30 min d'inactivité
        options.Cookie.Name = ".GestionProjets.Auth";
        options.Cookie.HttpOnly = true;
        // SameAsRequest : fonctionne en HTTP (intranet sans TLS) et HTTPS
        // Passer à Always uniquement si un reverse proxy avec certificat TLS est en place
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
    })
    .AddOpenIdConnect("AzureAd", options =>
    {
        var instance = builder.Configuration["AzureAd:Instance"]?.Trim();
        var tenantId = builder.Configuration["AzureAd:TenantId"]?.Trim();
        var clientId = builder.Configuration["AzureAd:ClientId"]?.Trim();
        var clientSecret = builder.Configuration["AzureAd:ClientSecret"]?.Trim();
        var callbackPath = builder.Configuration["AzureAd:CallbackPath"]?.Trim();
        var signedOutCallbackPath = builder.Configuration["AzureAd:SignedOutCallbackPath"]?.Trim();

        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.Authority = $"{(string.IsNullOrWhiteSpace(instance) ? "https://login.microsoftonline.com/" : instance.TrimEnd('/') + "/")}{tenantId}/v2.0";
        options.ClientId = clientId ?? string.Empty;
        options.ClientSecret = clientSecret ?? string.Empty;
        options.CallbackPath = string.IsNullOrWhiteSpace(callbackPath) ? "/signin-oidc" : callbackPath;
        options.SignedOutCallbackPath = string.IsNullOrWhiteSpace(signedOutCallbackPath) ? "/signout-callback-oidc" : signedOutCallbackPath;
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.RequireHttpsMetadata = true;
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

builder.Services.AddHsts(options =>
{
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

// Autorisation : par d�faut TOUT n�cessite d��tre authentifi�
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.Filters.Add(new AuthorizeFilter(policy));
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    options.Filters.AddService<PermissionMatrixAuthorizationFilter>();
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
        Log.Error(ex, "⚠️ Erreur lors de la migration EF Core — tentative SQL manuelle");
    }

    // Colonnes ajoutées par les nouvelles migrations — appliquées en SQL si Migrate() a échoué
    try
    {
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CharteProjets') AND name='SignatureImageCP')
                ALTER TABLE [CharteProjets] ADD [SignatureImageCP] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CharteProjets') AND name='SignatureImageDSI')
                ALTER TABLE [CharteProjets] ADD [SignatureImageDSI] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CharteProjets') AND name='SignatureImageSponsor')
                ALTER TABLE [CharteProjets] ADD [SignatureImageSponsor] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CharteProjets') AND name='DateSignatureImageCP')
                ALTER TABLE [CharteProjets] ADD [DateSignatureImageCP] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CharteProjets') AND name='DateSignatureImageDSI')
                ALTER TABLE [CharteProjets] ADD [DateSignatureImageDSI] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CharteProjets') AND name='DateSignatureImageSponsor')
                ALTER TABLE [CharteProjets] ADD [DateSignatureImageSponsor] datetime2 NULL;
        ");

        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId]='20260514065233_AddSignaturesElectroniquesCharte')
                INSERT INTO [__EFMigrationsHistory]([MigrationId],[ProductVersion]) VALUES('20260514065233_AddSignaturesElectroniquesCharte','9.0.11');
        ");
    }
    catch (Exception ex) { Log.Warning(ex, "Patch SignaturesCharte ignoré"); }

    try
    {
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('DemandesCreationCompte') AND type='U')
            BEGIN
                CREATE TABLE [DemandesCreationCompte] (
                    [Id] uniqueidentifier NOT NULL,
                    [Nom] nvarchar(max) NOT NULL,
                    [Prenoms] nvarchar(max) NOT NULL,
                    [Email] nvarchar(max) NOT NULL,
                    [Service] nvarchar(max) NOT NULL,
                    [DirectionId] uniqueidentifier NULL,
                    [DirecteurMetierId] uniqueidentifier NULL,
                    [Statut] int NOT NULL,
                    [CommentaireDM] nvarchar(max) NULL,
                    [CommentaireDSI] nvarchar(max) NULL,
                    [DateSoumission] datetime2 NOT NULL,
                    [UtilisateurCreePar] uniqueidentifier NULL,
                    [DateCreation] datetime2 NOT NULL,
                    [CreePar] nvarchar(max) NOT NULL,
                    [DateModification] datetime2 NULL,
                    [ModifiePar] nvarchar(max) NULL,
                    [EstSupprime] bit NOT NULL,
                    CONSTRAINT [PK_DemandesCreationCompte] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_DemandesCreationCompte_Directions] FOREIGN KEY ([DirectionId]) REFERENCES [Directions]([Id]),
                    CONSTRAINT [FK_DemandesCreationCompte_Utilisateurs] FOREIGN KEY ([DirecteurMetierId]) REFERENCES [Utilisateurs]([Id])
                );
                CREATE INDEX [IX_DemandesCreationCompte_DirecteurMetierId] ON [DemandesCreationCompte]([DirecteurMetierId]);
                CREATE INDEX [IX_DemandesCreationCompte_DirectionId] ON [DemandesCreationCompte]([DirectionId]);
            END
        ");
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId]='20260514084311_AddDemandeCreationCompte')
                INSERT INTO [__EFMigrationsHistory]([MigrationId],[ProductVersion]) VALUES('20260514084311_AddDemandeCreationCompte','9.0.0');
        ");
    }
    catch (Exception ex) { Log.Warning(ex, "Patch DemandeCreationCompte ignoré"); }

    try
    {
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Utilisateurs') AND name='ProfilRessource')
                ALTER TABLE [Utilisateurs] ADD [ProfilRessource] int NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='BilanPerimetre')
                ALTER TABLE [Projets] ADD [BilanPerimetre] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='BilanPlanning')
                ALTER TABLE [Projets] ADD [BilanPlanning] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='BilanBudget')
                ALTER TABLE [Projets] ADD [BilanBudget] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='BilanDifficultes')
                ALTER TABLE [Projets] ADD [BilanDifficultes] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='BilanReussites')
                ALTER TABLE [Projets] ADD [BilanReussites] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='LeconsReussites')
                ALTER TABLE [Projets] ADD [LeconsReussites] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='LeconsEchecs')
                ALTER TABLE [Projets] ADD [LeconsEchecs] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Projets') AND name='LeconsRecommandations')
                ALTER TABLE [Projets] ADD [LeconsRecommandations] nvarchar(max) NULL;
            ALTER TABLE [Projets] ALTER COLUMN [BilanCloture] nvarchar(max) NULL;
            ALTER TABLE [Projets] ALTER COLUMN [LeconsApprises] nvarchar(max) NULL;
        ");
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId]='20260514092743_AddStructuredBilanAndProfilRessource')
                INSERT INTO [__EFMigrationsHistory]([MigrationId],[ProductVersion]) VALUES('20260514092743_AddStructuredBilanAndProfilRessource','9.0.11');
        ");
        Log.Information("✅ Patch colonnes ProfilRessource / Bilan appliqué");
    }
    catch (Exception ex) { Log.Warning(ex, "Patch ProfilRessource/Bilan ignoré"); }

    try
    {
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='ActionsAVenirExecution')
                ALTER TABLE [FicheProjets] ADD [ActionsAVenirExecution] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='ActionsRealiseesExecution')
                ALTER TABLE [FicheProjets] ADD [ActionsRealiseesExecution] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='CanalCommunication')
                ALTER TABLE [FicheProjets] ADD [CanalCommunication] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='ChangeRequis')
                ALTER TABLE [FicheProjets] ADD [ChangeRequis] bit NOT NULL DEFAULT CAST(0 AS bit);
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='CommentaireAvancementExecution')
                ALTER TABLE [FicheProjets] ADD [CommentaireAvancementExecution] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='CommentaireBudgetPlanification')
                ALTER TABLE [FicheProjets] ADD [CommentaireBudgetPlanification] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='CommentaireStatutFinal')
                ALTER TABLE [FicheProjets] ADD [CommentaireStatutFinal] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='CommentaireValidationPlanification')
                ALTER TABLE [FicheProjets] ADD [CommentaireValidationPlanification] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='CopilPrevu')
                ALTER TABLE [FicheProjets] ADD [CopilPrevu] bit NOT NULL DEFAULT CAST(0 AS bit);
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='DateDebutRecette')
                ALTER TABLE [FicheProjets] ADD [DateDebutRecette] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='DateDebutReelleExecution')
                ALTER TABLE [FicheProjets] ADD [DateDebutReelleExecution] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='DateFinEstimeeExecution')
                ALTER TABLE [FicheProjets] ADD [DateFinEstimeeExecution] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='DateFinRecette')
                ALTER TABLE [FicheProjets] ADD [DateFinRecette] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='DateMepPrevue')
                ALTER TABLE [FicheProjets] ADD [DateMepPrevue] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='DecisionsExecution')
                ALTER TABLE [FicheProjets] ADD [DecisionsExecution] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='DecoupageLotsTravail')
                ALTER TABLE [FicheProjets] ADD [DecoupageLotsTravail] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='FrequenceReunions')
                ALTER TABLE [FicheProjets] ADD [FrequenceReunions] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='HypercareTermine')
                ALTER TABLE [FicheProjets] ADD [HypercareTermine] bit NOT NULL DEFAULT CAST(0 AS bit);
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='IncidentsMep')
                ALTER TABLE [FicheProjets] ADD [IncidentsMep] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='IncidentsPostMep')
                ALTER TABLE [FicheProjets] ADD [IncidentsPostMep] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='JalonsPrincipaux')
                ALTER TABLE [FicheProjets] ADD [JalonsPrincipaux] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='JustificationBudgetExecution')
                ALTER TABLE [FicheProjets] ADD [JustificationBudgetExecution] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='JustificationRetardExecution')
                ALTER TABLE [FicheProjets] ADD [JustificationRetardExecution] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='ParticipantsReunions')
                ALTER TABLE [FicheProjets] ADD [ParticipantsReunions] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='PerimetreTeste')
                ALTER TABLE [FicheProjets] ADD [PerimetreTeste] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='PeriodeHypercare')
                ALTER TABLE [FicheProjets] ADD [PeriodeHypercare] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='PlanMep')
                ALTER TABLE [FicheProjets] ADD [PlanMep] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='PlanRollback')
                ALTER TABLE [FicheProjets] ADD [PlanRollback] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='PlanificationRessources')
                ALTER TABLE [FicheProjets] ADD [PlanificationRessources] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='PrerequisMep')
                ALTER TABLE [FicheProjets] ADD [PrerequisMep] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='ProblemesBlocagesExecution')
                ALTER TABLE [FicheProjets] ADD [ProblemesBlocagesExecution] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='RaciParActivite')
                ALTER TABLE [FicheProjets] ADD [RaciParActivite] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='ReferenceChange')
                ALTER TABLE [FicheProjets] ADD [ReferenceChange] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='ResultatMep')
                ALTER TABLE [FicheProjets] ADD [ResultatMep] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='StatutFinalCloture')
                ALTER TABLE [FicheProjets] ADD [StatutFinalCloture] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='StatutHypercare')
                ALTER TABLE [FicheProjets] ADD [StatutHypercare] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='StatutValidationChange')
                ALTER TABLE [FicheProjets] ADD [StatutValidationChange] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='SyntheseChargesExecution')
                ALTER TABLE [FicheProjets] ADD [SyntheseChargesExecution] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='TransfertRunAcces')
                ALTER TABLE [FicheProjets] ADD [TransfertRunAcces] bit NOT NULL DEFAULT CAST(0 AS bit);
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='TransfertRunDocumentation')
                ALTER TABLE [FicheProjets] ADD [TransfertRunDocumentation] bit NOT NULL DEFAULT CAST(0 AS bit);
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='TransfertRunExploitationPrete')
                ALTER TABLE [FicheProjets] ADD [TransfertRunExploitationPrete] bit NOT NULL DEFAULT CAST(0 AS bit);
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='TransfertRunSupportInforme')
                ALTER TABLE [FicheProjets] ADD [TransfertRunSupportInforme] bit NOT NULL DEFAULT CAST(0 AS bit);
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='UtilisateursTesteurs')
                ALTER TABLE [FicheProjets] ADD [UtilisateursTesteurs] nvarchar(max) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('DemandesProjets') AND name='AutreSponsorId')
                ALTER TABLE [DemandesProjets] ADD [AutreSponsorId] uniqueidentifier NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('DemandesClotureProjets') AND name='CommentaireInitiateur')
                ALTER TABLE [DemandesClotureProjets] ADD [CommentaireInitiateur] nvarchar(max) NOT NULL DEFAULT N'';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ChargesProjets') AND name='ValideeParId')
                ALTER TABLE [ChargesProjets] ADD [ValideeParId] uniqueidentifier NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ChargesProjets') AND name='Activite')
                ALTER TABLE [ChargesProjets] ADD [Activite] nvarchar(max) NOT NULL DEFAULT N'';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ChargesProjets') AND name='TypeActivite')
                ALTER TABLE [ChargesProjets] ADD [TypeActivite] nvarchar(max) NOT NULL DEFAULT N'';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ChargesProjets') AND name='CommentaireValidation')
                ALTER TABLE [ChargesProjets] ADD [CommentaireValidation] nvarchar(max) NOT NULL DEFAULT N'';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ChargesProjets') AND name='DateSoumissionValidation')
                ALTER TABLE [ChargesProjets] ADD [DateSoumissionValidation] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ChargesProjets') AND name='DateValidation')
                ALTER TABLE [ChargesProjets] ADD [DateValidation] datetime2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ChargesProjets') AND name='StatutValidation')
                ALTER TABLE [ChargesProjets] ADD [StatutValidation] int NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('AnomaliesProjets') AND name='CasTestProjetId')
                ALTER TABLE [AnomaliesProjets] ADD [CasTestProjetId] uniqueidentifier NULL;
        ");
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId]='20260519135253_AddComplementPhaseAndChargeWorkflowFields')
               AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='ActionsAVenirExecution')
               AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FicheProjets') AND name='TransfertRunSupportInforme')
               AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ChargesProjets') AND name='ValideeParId')
               AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('DemandesClotureProjets') AND name='CommentaireInitiateur')
               AND EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('CampagnesTestsProjets') AND type='U')
               AND EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('CasTestsProjets') AND type='U')
               AND EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('CollaborationsProjets') AND type='U')
               AND EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('DossiersSignatureProjets') AND type='U')
               AND EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('RolePermissions') AND type='U')
                INSERT INTO [__EFMigrationsHistory]([MigrationId],[ProductVersion]) VALUES('20260519135253_AddComplementPhaseAndChargeWorkflowFields','9.0.11');
        ");
        Log.Information("âœ… Patch colonnes complement phases / charges appliquÃ©");
    }
    catch (Exception ex) { Log.Warning(ex, "Patch complement phases/charges ignorÃ©"); }

    try
    {
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('TachesPlanningProjets') AND type='U')
            BEGIN
                CREATE TABLE [TachesPlanningProjets] (
                    [Id] uniqueidentifier NOT NULL,
                    [ProjetId] uniqueidentifier NOT NULL,
                    [CodeWbs] nvarchar(64) NOT NULL DEFAULT N'',
                    [Libelle] nvarchar(256) NOT NULL DEFAULT N'',
                    [Responsable] nvarchar(256) NOT NULL DEFAULT N'',
                    [Dependances] nvarchar(max) NOT NULL DEFAULT N'',
                    [Commentaire] nvarchar(max) NOT NULL DEFAULT N'',
                    [DateDebutPrevue] datetime2 NOT NULL,
                    [DateFinPrevue] datetime2 NOT NULL,
                    [Avancement] int NOT NULL DEFAULT 0,
                    [Ordre] int NOT NULL DEFAULT 0,
                    [EstJalon] bit NOT NULL DEFAULT CAST(0 AS bit),
                    [DateCreation] datetime2 NOT NULL,
                    [CreePar] nvarchar(max) NOT NULL,
                    [DateModification] datetime2 NULL,
                    [ModifiePar] nvarchar(max) NULL,
                    [EstSupprime] bit NOT NULL DEFAULT CAST(0 AS bit),
                    CONSTRAINT [PK_TachesPlanningProjets] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_TachesPlanningProjets_Projets] FOREIGN KEY ([ProjetId]) REFERENCES [Projets]([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_TachesPlanningProjets_ProjetId_Ordre] ON [TachesPlanningProjets]([ProjetId], [Ordre]);
            END
        ");
        Log.Information("Patch table TachesPlanningProjets appliqué");
    }
    catch (Exception ex) { Log.Warning(ex, "Patch TachesPlanningProjets ignoré"); }

    try
    {
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('LignesRaciProjets') AND type='U')
            BEGIN
                CREATE TABLE [LignesRaciProjets] (
                    [Id] uniqueidentifier NOT NULL,
                    [ProjetId] uniqueidentifier NOT NULL,
                    [CodeActivite] nvarchar(64) NOT NULL DEFAULT N'',
                    [Activite] nvarchar(256) NOT NULL DEFAULT N'',
                    [Responsable] nvarchar(256) NOT NULL DEFAULT N'',
                    [Approbateur] nvarchar(256) NOT NULL DEFAULT N'',
                    [Consulte] nvarchar(max) NOT NULL DEFAULT N'',
                    [Informe] nvarchar(max) NOT NULL DEFAULT N'',
                    [Ordre] int NOT NULL DEFAULT 0,
                    [DateCreation] datetime2 NOT NULL,
                    [CreePar] nvarchar(max) NOT NULL,
                    [DateModification] datetime2 NULL,
                    [ModifiePar] nvarchar(max) NULL,
                    [EstSupprime] bit NOT NULL DEFAULT CAST(0 AS bit),
                    CONSTRAINT [PK_LignesRaciProjets] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_LignesRaciProjets_Projets] FOREIGN KEY ([ProjetId]) REFERENCES [Projets]([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_LignesRaciProjets_ProjetId_Ordre] ON [LignesRaciProjets]([ProjetId], [Ordre]);
            END
            ELSE IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('LignesRaciProjets') AND name='CodeActivite')
            BEGIN
                ALTER TABLE [LignesRaciProjets] ADD [CodeActivite] nvarchar(64) NOT NULL DEFAULT N'';
            END

            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('LignesCommunicationProjets') AND type='U')
            BEGIN
                CREATE TABLE [LignesCommunicationProjets] (
                    [Id] uniqueidentifier NOT NULL,
                    [ProjetId] uniqueidentifier NOT NULL,
                    [Instance] nvarchar(256) NOT NULL DEFAULT N'',
                    [Objectif] nvarchar(max) NOT NULL DEFAULT N'',
                    [Frequence] nvarchar(256) NOT NULL DEFAULT N'',
                    [Canal] nvarchar(256) NOT NULL DEFAULT N'',
                    [Participants] nvarchar(max) NOT NULL DEFAULT N'',
                    [Responsable] nvarchar(256) NOT NULL DEFAULT N'',
                    [EstCopil] bit NOT NULL DEFAULT CAST(0 AS bit),
                    [Ordre] int NOT NULL DEFAULT 0,
                    [DateCreation] datetime2 NOT NULL,
                    [CreePar] nvarchar(max) NOT NULL,
                    [DateModification] datetime2 NULL,
                    [ModifiePar] nvarchar(max) NULL,
                    [EstSupprime] bit NOT NULL DEFAULT CAST(0 AS bit),
                    CONSTRAINT [PK_LignesCommunicationProjets] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_LignesCommunicationProjets_Projets] FOREIGN KEY ([ProjetId]) REFERENCES [Projets]([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_LignesCommunicationProjets_ProjetId_Ordre] ON [LignesCommunicationProjets]([ProjetId], [Ordre]);
            END

            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('LignesBudgetPlanificationProjets') AND type='U')
            BEGIN
                CREATE TABLE [LignesBudgetPlanificationProjets] (
                    [Id] uniqueidentifier NOT NULL,
                    [ProjetId] uniqueidentifier NOT NULL,
                    [Poste] nvarchar(256) NOT NULL DEFAULT N'',
                    [Description] nvarchar(max) NOT NULL DEFAULT N'',
                    [Montant] decimal(18,2) NOT NULL DEFAULT 0,
                    [Commentaire] nvarchar(max) NOT NULL DEFAULT N'',
                    [Ordre] int NOT NULL DEFAULT 0,
                    [DateCreation] datetime2 NOT NULL,
                    [CreePar] nvarchar(max) NOT NULL,
                    [DateModification] datetime2 NULL,
                    [ModifiePar] nvarchar(max) NULL,
                    [EstSupprime] bit NOT NULL DEFAULT CAST(0 AS bit),
                    CONSTRAINT [PK_LignesBudgetPlanificationProjets] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_LignesBudgetPlanificationProjets_Projets] FOREIGN KEY ([ProjetId]) REFERENCES [Projets]([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_LignesBudgetPlanificationProjets_ProjetId_Ordre] ON [LignesBudgetPlanificationProjets]([ProjetId], [Ordre]);
            END

            IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID('PvKickOffProjets') AND type='U')
            BEGIN
                CREATE TABLE [PvKickOffProjets] (
                    [Id] uniqueidentifier NOT NULL,
                    [ProjetId] uniqueidentifier NOT NULL,
                    [DateReunion] datetime2 NULL,
                    [Heure] nvarchar(32) NOT NULL DEFAULT N'',
                    [Lieu] nvarchar(256) NOT NULL DEFAULT N'',
                    [Animateur] nvarchar(256) NOT NULL DEFAULT N'',
                    [Objectifs] nvarchar(max) NOT NULL DEFAULT N'',
                    [Participants] nvarchar(max) NOT NULL DEFAULT N'',
                    [OrdreDuJour] nvarchar(max) NOT NULL DEFAULT N'',
                    [Decisions] nvarchar(max) NOT NULL DEFAULT N'',
                    [Actions] nvarchar(max) NOT NULL DEFAULT N'',
                    [Commentaires] nvarchar(max) NOT NULL DEFAULT N'',
                    [DateCreation] datetime2 NOT NULL,
                    [CreePar] nvarchar(max) NOT NULL,
                    [DateModification] datetime2 NULL,
                    [ModifiePar] nvarchar(max) NULL,
                    [EstSupprime] bit NOT NULL DEFAULT CAST(0 AS bit),
                    CONSTRAINT [PK_PvKickOffProjets] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PvKickOffProjets_Projets] FOREIGN KEY ([ProjetId]) REFERENCES [Projets]([Id]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX [IX_PvKickOffProjets_ProjetId] ON [PvKickOffProjets]([ProjetId]);
            END
        ");
        Log.Information("Patch tables planification native appliqué");
    }
    catch (Exception ex) { Log.Warning(ex, "Patch planification native ignoré"); }

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
            if (app.Configuration.GetValue<bool>("Security:DisplayBootstrapPasswordsInConsole"))
            {
                // Affichage console uniquement — jamais persisté sur disque
                Console.WriteLine("=================================================");
                Console.WriteLine("MOT DE PASSE ADMIN TEMPORAIRE : " + randomPassword);
                Console.WriteLine("Changez ce mot de passe dès la première connexion.");
                Console.WriteLine("=================================================");
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
                if (app.Configuration.GetValue<bool>("Security:DisplayBootstrapPasswordsInConsole"))
                {
                    Console.WriteLine("=================================================");
                    Console.WriteLine("MOT DE PASSE ADMIN TEMPORAIRE : " + randomPassword);
                    Console.WriteLine("Changez ce mot de passe dès la première connexion.");
                    Console.WriteLine("=================================================");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Erreur lors de la création de l'utilisateur admin");
            }
        }
    }
}

// Seed de données de démonstration (activé via SeedDemo:Enabled dans appsettings)
if (bool.TryParse(app.Configuration["SeedDemo:Enabled"], out var seedEnabled) && seedEnabled)
{
    using var seedScope = app.Services.CreateScope();
    var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        await GestionProjects.Infrastructure.Persistence.SeedDonneesDemo.ExecuterAsync(seedDb);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "⚠️ Le seed de démonstration a rencontré une erreur (non bloquant)");
    }
}

// Middleware de gestion d'erreurs global (doit être avant les autres middlewares)
app.UseWhen(
    context => ShouldRenderHtmlErrorPage(context.Request),
    htmlPipeline => htmlPipeline.UseStatusCodePagesWithReExecute("/Home/Error/{0}"));
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Middleware pour les headers de sécurité HTTP
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Localisation
var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(localizationOptions);

// app.UseHttpsRedirection(); // Désactivé : l'app tourne en HTTP sur le réseau intranet

// Bloquer l'accès non authentifié aux fichiers uploadés
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads"))
    {
        var authResult = await context.AuthenticateAsync(
            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authResult.Succeeded)
        {
            context.Response.StatusCode = 401;
            context.Response.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(context.Request.Path)}");
            return;
        }
    }
    await next();
});

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

static bool ShouldRenderHtmlErrorPage(Microsoft.AspNetCore.Http.HttpRequest request)
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

// Fonction helper pour générer un mot de passe sécurisé
static string GenerateSecurePassword()
{
    const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    const string lowercase = "abcdefghijklmnopqrstuvwxyz";
    const string digits = "0123456789";
    const string special = "@#$%&*!";
    const string allChars = uppercase + lowercase + digits + special;

    var password = new System.Text.StringBuilder();
    password.Append(uppercase[GetSecureRandomIndex(uppercase.Length)]);
    password.Append(lowercase[GetSecureRandomIndex(lowercase.Length)]);
    password.Append(digits[GetSecureRandomIndex(digits.Length)]);
    password.Append(special[GetSecureRandomIndex(special.Length)]);

    for (int i = 0; i < 8; i++)
        password.Append(allChars[GetSecureRandomIndex(allChars.Length)]);

    var chars = password.ToString().ToCharArray();
    for (int i = chars.Length - 1; i > 0; i--)
    {
        int j = GetSecureRandomIndex(i + 1);
        (chars[i], chars[j]) = (chars[j], chars[i]);
    }

    return new string(chars);
}

static int GetSecureRandomIndex(int max)
{
    return (int)(System.Security.Cryptography.RandomNumberGenerator.GetInt32(max));
}

public partial class Program { }
