using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Infrastructure.Services;

namespace GestionProjects.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddMemoryCache();
        services.AddScoped<ICacheService, CacheService>();
        services.AddScoped<IAideQueryService, AideQueryService>();
        services.AddScoped<IAzureAuthWorkflowService, AzureAuthWorkflowService>();
        services.AddScoped<IDashboardAnalyticsService, DashboardAnalyticsService>();
        services.AddScoped<IHomeDashboardService, HomeDashboardService>();

        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<IDocumentAccessService, DocumentAccessService>();
        services.AddScoped<IDocumentPreviewService, DocumentPreviewService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IPdfService, PdfService>();
        services.AddScoped<IExcelService, ExcelService>();
        services.AddScoped<IWordService, WordService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationQueryService, NotificationQueryService>();
        services.AddScoped<INotificationTargetResolver, NotificationTargetResolver>();
        services.AddScoped<ILivrableValidationService, LivrableValidationService>();
        services.AddScoped<ILivrableProjetService, LivrableProjetService>();
        services.AddScoped<IChargeProjetService, ChargeProjetService>();
        services.AddScoped<IMembreProjetService, MembreProjetService>();
        services.AddScoped<IProjetDetailsWorkflowService, ProjetDetailsWorkflowService>();
        services.AddScoped<IFicheProjetService, FicheProjetService>();
        services.AddScoped<IRAGCalculationService, RAGCalculationService>();
        services.AddScoped<ITeamsNotificationService, TeamsNotificationService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IPasswordSetupTokenService, PasswordSetupTokenService>();
        services.AddScoped<IUtilisateurIdentityResolver, UtilisateurIdentityResolver>();
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
        services.AddScoped<IAutorisationMatrixService, AutorisationMatrixService>();
        services.AddScoped<IDemandeCompteAdminService, DemandeCompteAdminService>();
        services.AddScoped<IDemandeCreationCompteWorkflowService, DemandeCreationCompteWorkflowService>();
        services.AddScoped<IDemandeAccesQueryService, DemandeAccesQueryService>();
        services.AddScoped<IDemandeAccesWorkflowService, DemandeAccesWorkflowService>();
        services.AddScoped<IDelegationAdminService, DelegationAdminService>();
        services.AddScoped<IDemandeProjetQueryService, DemandeProjetQueryService>();
        services.AddScoped<IDemandeProjetWorkflowService, DemandeProjetWorkflowService>();
        services.AddScoped<IClotureProjetWorkflowService, ClotureProjetWorkflowService>();
        services.AddScoped<ICharteProjetWorkflowService, CharteProjetWorkflowService>();
        services.AddScoped<IUatProjetWorkflowService, UatProjetWorkflowService>();

        services.AddHttpClient("Teams")
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));

        return services;
    }
}
