using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Infrastructure.Security;
using GestionProjects.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace GestionProjects.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddMemoryCache();
        services.AddScoped<ICacheService, CacheService>();

        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<IDocumentPreviewService, DocumentPreviewService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IPdfService, PdfService>();
        services.AddScoped<IExcelService, ExcelService>();
        services.AddScoped<IWordService, WordService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ILivrableValidationService, LivrableValidationService>();
        services.AddScoped<IRAGCalculationService, RAGCalculationService>();
        services.AddScoped<ITeamsNotificationService, TeamsNotificationService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IPasswordSetupTokenService, PasswordSetupTokenService>();
        services.AddScoped<IUtilisateurService, UtilisateurService>();
        services.AddScoped<IProjetQueryService, ProjetQueryService>();
        services.AddScoped<IUatValidationService, UatValidationService>();
        services.AddScoped<ICollaborationProjetService, CollaborationProjetService>();
        services.AddScoped<IElectronicSignatureService, ElectronicSignatureService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IDirectionAdminService, DirectionAdminService>();
        services.AddScoped<IServiceAdminService, ServiceAdminService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IRoleAdminService, RoleAdminService>();
        services.AddScoped<IParametreAdminService, ParametreAdminService>();
        services.AddScoped<IDemandeCompteAdminService, DemandeCompteAdminService>();
        services.AddScoped<IDelegationAdminService, DelegationAdminService>();
        services.AddScoped<IDemandeProjetQueryService, DemandeProjetQueryService>();
        services.AddScoped<IDemandeProjetWorkflowService, DemandeProjetWorkflowService>();
        services.AddScoped<PermissionMatrixAuthorizationFilter>();

        services.AddHttpClient("Teams")
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));

        return services;
    }

    public static IServiceCollection AddControllersWithGlobalFilters(this IServiceCollection services)
    {
        services.AddControllersWithViews(options =>
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.Filters.Add(new AuthorizeFilter(policy));
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            options.Filters.AddService<PermissionMatrixAuthorizationFilter>();
        });

        return services;
    }
}
