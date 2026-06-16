using GestionProjects.Application.Common.Constants;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestionProjects.Infrastructure.Services;

public sealed class DemandeAccesWorkflowService : IDemandeAccesWorkflowService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private readonly IPasswordSetupTokenService _passwordSetupTokenService;
    private readonly IUtilisateurIdentityResolver _identityResolver;
    private readonly IConfiguration _configuration;

    public DemandeAccesWorkflowService(
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IAuditService auditService,
        IEmailService emailService,
        INotificationService notificationService,
        IPasswordSetupTokenService passwordSetupTokenService,
        IUtilisateurIdentityResolver identityResolver,
        IConfiguration configuration)
    {
        _db = db;
        _currentUserService = currentUserService;
        _auditService = auditService;
        _emailService = emailService;
        _notificationService = notificationService;
        _passwordSetupTokenService = passwordSetupTokenService;
        _identityResolver = identityResolver;
        _configuration = configuration;
    }

    public async Task<DemandeAccesWorkflowResult> SoumettreDemandeLocaleAsync(SoumettreDemandeAccesLocaleInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Nom) ||
            string.IsNullOrWhiteSpace(input.Prenoms) ||
            string.IsNullOrWhiteSpace(input.Email) ||
            string.IsNullOrWhiteSpace(input.Matricule) ||
            string.IsNullOrWhiteSpace(input.RolesSouhaites))
        {
            return DemandeAccesWorkflowResult.Error("Tous les champs obligatoires doivent être remplis.");
        }

        if (input.DirectionId == Guid.Empty)
        {
            return DemandeAccesWorkflowResult.Error("Merci de sélectionner votre direction de rattachement.");
        }

        var directionExiste = await _db.Directions
            .AnyAsync(d => d.Id == input.DirectionId && !d.EstSupprime && d.EstActive);
        if (!directionExiste)
        {
            return DemandeAccesWorkflowResult.Error("La direction sélectionnée est invalide ou inactive.");
        }

        // Un compte rattache a une direction sans Directeur Metier laisse le workflow
        // d'approbation aveugle (personne a notifier / a faire valider) : on refuse net.
        var directionADm = await _db.Utilisateurs.AnyAsync(u =>
            !u.EstSupprime &&
            u.DirectionId == input.DirectionId &&
            u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier));
        if (!directionADm)
        {
            return DemandeAccesWorkflowResult.Error("Cette direction n'a pas de Directeur Métier rattaché. Contactez la DSI pour qu'un DM y soit affecté avant de soumettre votre demande.");
        }

        var nomNormalise = input.Nom.Trim();
        var prenomsNormalise = input.Prenoms.Trim();
        var emailNormalise = input.Email.Trim();
        var matriculeNormalise = input.Matricule.Trim();
        var roleSouhaiteNormalise = input.RolesSouhaites.Trim();

        var demandeExiste = await _db.DemandesAccesAzureAd
            .AnyAsync(d => d.Statut == StatutDemandeAcces.EnAttente &&
                           (d.Email == emailNormalise || d.Matricule == matriculeNormalise));
        if (demandeExiste)
        {
            return DemandeAccesWorkflowResult.Info("Une demande d'accès est déjà en attente de traitement pour ces informations.");
        }

        var demandeAcces = new DemandeAccesAzureAd
        {
            Id = Guid.NewGuid(),
            Nom = nomNormalise,
            Prenoms = prenomsNormalise,
            Email = emailNormalise,
            Matricule = matriculeNormalise,
            Justification = BuildLocalAccessRequestJustification(roleSouhaiteNormalise, input.Message),
            AzureDepartment = AccessRequestConstants.LocalAzureDepartment,
            DirectionDetecteeId = input.DirectionId,
            Statut = StatutDemandeAcces.EnAttente,
            DateCreation = DateTime.Now,
            CreePar = "ANONYMOUS",
            EstSupprime = false
        };

        _db.DemandesAccesAzureAd.Add(demandeAcces);
        await _db.SaveChangesAsync();

        await _notificationService.NotifierRoleAsync(
            RoleUtilisateur.AdminIT,
            TypeNotification.DemandeSupportTechnique,
            "Nouvelle demande d'accès local CIT",
            $"Demande d'accès local CIT de {demandeAcces.Prenoms} {demandeAcces.Nom} ({demandeAcces.Email}). Rôle souhaité : {roleSouhaiteNormalise}.",
            DomainEntityTypes.DemandeAccesAzureAd,
            demandeAcces.Id,
            new
            {
                demandeAcces.Email,
                demandeAcces.Matricule,
                TypeAcces = AccessRequestConstants.LocalAccountLabel,
                RoleSouhaite = roleSouhaiteNormalise,
                Message = input.Message?.Trim()
            });

        // Information aux Directeurs Metier de la direction rattachee (ils ne sont pas
        // validateurs mais informes pour pouvoir signaler une anomalie a l'AdminIT).
        await NotifierDirecteursMetierAsync(demandeAcces, roleSouhaiteNormalise);

        return DemandeAccesWorkflowResult.Success(
            "Votre demande d'accès a été envoyée à l'administrateur. Vous serez contacté prochainement.",
            demandeAcces.Id);
    }

    private async Task NotifierDirecteursMetierAsync(
        DemandeAccesAzureAd demande,
        string roleSouhaite)
    {
        if (!demande.DirectionDetecteeId.HasValue)
            return;

        var direction = await _db.Directions
            .FirstOrDefaultAsync(d => d.Id == demande.DirectionDetecteeId.Value && !d.EstSupprime);
        if (direction == null)
            return;

        var dms = await _db.Utilisateurs
            .Where(u => !u.EstSupprime &&
                        u.DirectionId == direction.Id &&
                        u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.DirecteurMetier))
            .ToListAsync();

        var nomDemandeur = $"{demande.Prenoms} {demande.Nom}".Trim();
        foreach (var dm in dms)
        {
            await _notificationService.NotifierUtilisateurAsync(
                dm.Id,
                TypeNotification.DemandeSupportTechnique,
                $"Demande d'accès dans votre direction — {nomDemandeur}",
                $"{nomDemandeur} ({demande.Email}) demande un accès rattaché à {direction.Libelle}. Rôle souhaité : {roleSouhaite}. Information uniquement — l'AdminIT traitera la demande.",
                DomainEntityTypes.DemandeAccesAzureAd,
                demande.Id);

            if (!string.IsNullOrWhiteSpace(dm.Email))
            {
                await _emailService.EnvoyerDemandeAccesAuDmAsync(
                    dm.Email,
                    $"{dm.Prenoms} {dm.Nom}".Trim(),
                    nomDemandeur,
                    demande.Email,
                    direction.Libelle,
                    roleSouhaite);
            }
        }
    }

    public async Task<DemandeAccesWorkflowResult> ApprouverAsync(ApprouverDemandeAccesInput input)
    {
        var demande = await _db.DemandesAccesAzureAd
            .FirstOrDefaultAsync(d => d.Id == input.DemandeId);

        if (demande == null)
        {
            return DemandeAccesWorkflowResult.Error("Demande introuvable.");
        }

        if (demande.Statut != StatutDemandeAcces.EnAttente)
        {
            return DemandeAccesWorkflowResult.Error("Cette demande a déjà été traitée.", demande.Id);
        }

        var roleAAttribuer = NormalizeAccessRequestRole(input.Role);
        var isLocalRequest = string.Equals(demande.AzureDepartment, AccessRequestConstants.LocalAzureDepartment, StringComparison.OrdinalIgnoreCase);
        var utilisateurCree = false;
        var emailDemande = demande.Email.Trim();
        var matriculeDemande = demande.Matricule.Trim();

        var resolutionMode = isLocalRequest
            ? UtilisateurIdentityResolutionMode.Strict
            : UtilisateurIdentityResolutionMode.PreferEmail;
        var resolution = await _identityResolver.ResolveActiveUserAsync(
            emailDemande,
            matriculeDemande,
            resolutionMode,
            includeRoles: true);

        if (resolution.HasError)
        {
            return DemandeAccesWorkflowResult.Error($"Impossible d'approuver : {resolution.ErrorMessage}", demande.Id);
        }

        var utilisateur = resolution.Utilisateur;

        if (utilisateur == null)
        {
            utilisateur = new Utilisateur
            {
                Id = Guid.NewGuid(),
                Matricule = matriculeDemande,
                MotDePasse = isLocalRequest ? string.Empty : BCrypt.Net.BCrypt.HashPassword($"{Guid.NewGuid():N}Aa1!"),
                Nom = demande.Nom,
                Prenoms = demande.Prenoms,
                Email = emailDemande,
                DirectionId = input.DirectionId ?? demande.DirectionDetecteeId,
                PeutCreerDemandeProjet = true,
                CreePar = _currentUserService.Matricule ?? "SYSTEM"
            };
            utilisateurCree = true;

            _db.Utilisateurs.Add(utilisateur);
        }
        else
        {
            utilisateur.Nom = string.IsNullOrWhiteSpace(utilisateur.Nom) ? demande.Nom : utilisateur.Nom;
            utilisateur.Prenoms = string.IsNullOrWhiteSpace(utilisateur.Prenoms) ? demande.Prenoms : utilisateur.Prenoms;
            utilisateur.Matricule = string.IsNullOrWhiteSpace(utilisateur.Matricule) ? matriculeDemande : utilisateur.Matricule;
            utilisateur.Email = string.IsNullOrWhiteSpace(utilisateur.Email) ? emailDemande : utilisateur.Email;
            utilisateur.DirectionId = input.DirectionId ?? demande.DirectionDetecteeId ?? utilisateur.DirectionId;
            utilisateur.PeutCreerDemandeProjet = true;
        }

        var aRoleAttribue = utilisateur.UtilisateurRoles.Any(ur =>
            !ur.EstSupprime &&
            ur.Role == roleAAttribuer &&
            (!ur.DateDebut.HasValue || ur.DateDebut.Value <= DateTime.Now) &&
            (!ur.DateFin.HasValue || ur.DateFin.Value >= DateTime.Now));

        if (!aRoleAttribue)
        {
            _db.UtilisateurRoles.Add(new UtilisateurRole
            {
                Id = Guid.NewGuid(),
                UtilisateurId = utilisateur.Id,
                Role = roleAAttribuer,
                DateDebut = DateTime.Now,
                CreePar = _currentUserService.Matricule ?? "SYSTEM"
            });
        }

        demande.Statut = StatutDemandeAcces.Approuvee;
        demande.DirectionDetecteeId = input.DirectionId ?? demande.DirectionDetecteeId;
        demande.CommentaireTraitement = input.Commentaire?.Trim() ?? string.Empty;
        demande.DateTraitement = DateTime.Now;
        demande.TraiteParId = input.TraiteParId;
        demande.UtilisateurCreeId = utilisateur.Id;

        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync(
            "APPROBATION_DEMANDE_ACCES_AZURE_AD",
            DomainEntityTypes.DemandeAccesAzureAd,
            demande.Id,
            null,
            new
            {
                demande.Email,
                UtilisateurId = utilisateur.Id,
                DirectionId = demande.DirectionDetecteeId,
                Role = roleAAttribuer.ToString()
            });

        if (isLocalRequest && (utilisateurCree || string.IsNullOrWhiteSpace(utilisateur.MotDePasse)))
        {
            var jeton = await _passwordSetupTokenService.CreerAsync(
                utilisateur.Id,
                _currentUserService.Matricule ?? "SYSTEM");
            await _db.SaveChangesAsync();

            var nomComplet = $"{demande.Prenoms} {demande.Nom}".Trim();
            await _emailService.EnvoyerActivationCompteAsync(
                demande.Email,
                nomComplet,
                utilisateur.Matricule,
                BuildActivationLink(utilisateur.Id, jeton.Token),
                jeton.DateExpiration);

            return DemandeAccesWorkflowResult.Success(
                $"Demande approuvée, compte local créé avec le rôle {roleAAttribuer}, lien d'activation envoyé.",
                demande.Id);
        }

        var loginInstruction = isLocalRequest
            ? "Vous pouvez désormais vous connecter avec votre compte local CIT."
            : "Vous pouvez désormais vous connecter avec votre compte Microsoft 365 CIT.";

        var body = $"""
            <p>Bonjour {demande.Prenoms} {demande.Nom},</p>
            <p>Votre demande d'accès à l'application Zéïnab a été approuvée.</p>
            <p>{loginInstruction}</p>
            {(string.IsNullOrWhiteSpace(demande.CommentaireTraitement) ? string.Empty : $"<p>Commentaire administrateur : {demande.CommentaireTraitement}</p>")}
            <p>Cordialement,<br />DSI - Zéïnab</p>
            """;

        await _emailService.SendEmailAsync(
            demande.Email,
            "Accès approuvé - Zéïnab",
            body,
            $"Bonjour {demande.Prenoms} {demande.Nom}, votre demande d'accès a été approuvée.");

        return DemandeAccesWorkflowResult.Success(
            $"Demande approuvée et accès utilisateur préparé avec le rôle {roleAAttribuer}.",
            demande.Id);
    }

    public async Task<DemandeAccesWorkflowResult> RejeterAsync(RejeterDemandeAccesInput input)
    {
        var demande = await _db.DemandesAccesAzureAd
            .FirstOrDefaultAsync(d => d.Id == input.DemandeId);

        if (demande == null)
        {
            return DemandeAccesWorkflowResult.Error("Demande introuvable.");
        }

        if (demande.Statut != StatutDemandeAcces.EnAttente)
        {
            return DemandeAccesWorkflowResult.Error("Cette demande a déjà été traitée.", demande.Id);
        }

        if (string.IsNullOrWhiteSpace(input.Commentaire))
        {
            return DemandeAccesWorkflowResult.Error("Le commentaire de rejet est obligatoire.", demande.Id);
        }

        demande.Statut = StatutDemandeAcces.Rejetee;
        demande.CommentaireTraitement = input.Commentaire.Trim();
        demande.DateTraitement = DateTime.Now;
        demande.TraiteParId = input.TraiteParId;

        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync(
            "REJET_DEMANDE_ACCES_AZURE_AD",
            DomainEntityTypes.DemandeAccesAzureAd,
            demande.Id,
            null,
            new { demande.Email, Commentaire = demande.CommentaireTraitement });

        var body = $"""
            <p>Bonjour {demande.Prenoms} {demande.Nom},</p>
            <p>Votre demande d'accès à l'application Zéïnab n'a pas été approuvée.</p>
            <p>Commentaire administrateur : {demande.CommentaireTraitement}</p>
            <p>Cordialement,<br />DSI - Zéïnab</p>
            """;

        await _emailService.SendEmailAsync(
            demande.Email,
            "Accès refusé - Zéïnab",
            body,
            $"Bonjour {demande.Prenoms} {demande.Nom}, votre demande d'accès a été refusée. Commentaire : {demande.CommentaireTraitement}");

        return DemandeAccesWorkflowResult.Success("Demande rejetée.", demande.Id);
    }

    private static RoleUtilisateur NormalizeAccessRequestRole(RoleUtilisateur role)
    {
        return role switch
        {
            RoleUtilisateur.DirecteurMetier => RoleUtilisateur.DirecteurMetier,
            RoleUtilisateur.ChefDeProjet => RoleUtilisateur.ChefDeProjet,
            RoleUtilisateur.ResponsableSolutionsIT => RoleUtilisateur.ResponsableSolutionsIT,
            _ => RoleUtilisateur.Demandeur
        };
    }

    private static string BuildLocalAccessRequestJustification(string roleSouhaite, string? message)
    {
        var justification = $"Rôle souhaité : {roleSouhaite}";
        var messageNormalise = message?.Trim();

        return string.IsNullOrWhiteSpace(messageNormalise)
            ? justification
            : $"{justification}{Environment.NewLine}Message : {messageNormalise}";
    }

    private string BuildActivationLink(Guid utilisateurId, string token)
    {
        var baseUrl = (_configuration["SmtpSettings:BaseUrl"] ?? string.Empty).Trim().TrimEnd('/');
        var path = $"/Account/InitialiserMotDePasse?utilisateurId={utilisateurId}&token={Uri.EscapeDataString(token)}";
        return string.IsNullOrWhiteSpace(baseUrl) ? path : $"{baseUrl}{path}";
    }
}
