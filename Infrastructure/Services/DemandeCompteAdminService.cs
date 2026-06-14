using GestionProjects.Application.Common.Constants;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestionProjects.Infrastructure.Services;

public class DemandeCompteAdminService : IDemandeCompteAdminService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly IPasswordSetupTokenService _passwordSetupTokenService;
    private readonly IConfiguration _configuration;

    public DemandeCompteAdminService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditService audit,
        IEmailService email,
        IPasswordSetupTokenService passwordSetupTokenService,
        IConfiguration configuration)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _email = email;
        _passwordSetupTokenService = passwordSetupTokenService;
        _configuration = configuration;
    }

    public async Task<List<DemandeCreationCompte>> GetListAsync(Guid? restrictToDmId)
    {
        IQueryable<DemandeCreationCompte> query = _db.DemandesCreationCompte
            .Include(d => d.Direction)
            .Include(d => d.DirecteurMetier)
            .Where(d => !d.EstSupprime);

        if (restrictToDmId.HasValue)
            query = query.Where(d => d.DirecteurMetierId == restrictToDmId.Value);

        return await query.OrderByDescending(d => d.DateSoumission).ToListAsync();
    }

    public async Task<WorkflowResult> ValiderDmAsync(Guid id, string? commentaire, Guid currentUserId, bool hasFullScope, string nomActeur)
    {
        var demande = await _db.DemandesCreationCompte
            .Include(d => d.Direction)
            .Include(d => d.DirecteurMetier)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (demande == null) return WorkflowResult.NotFound();

        if (!hasFullScope && demande.DirecteurMetierId != currentUserId)
            return WorkflowResult.Forbidden();

        if (demande.Statut != StatutDemandeCompte.EnAttenteValidationDM)
            return WorkflowResult.Error("Cette demande n'est pas en attente de validation DM.");

        demande.Statut           = StatutDemandeCompte.ValideeParDM;
        demande.CommentaireDM     = commentaire;
        demande.DateModification = DateTime.Now;
        demande.ModifiePar       = _currentUser.Matricule;
        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("VALIDATION_DM_COMPTE", DomainEntityTypes.DemandeCreationCompte, demande.Id);

        var roles = new[] { RoleUtilisateur.DSI, RoleUtilisateur.AdminIT, RoleUtilisateur.ResponsableSolutionsIT };
        var destinataires = await _db.Utilisateurs
            .Where(u => !u.EstSupprime)
            .Join(_db.UtilisateurRoles.Where(r => roles.Contains(r.Role) && !r.EstSupprime),
                  u => u.Id, r => r.UtilisateurId, (u, r) => u)
            .Where(u => u.Email != null)
            .Select(u => u.Email!)
            .Distinct()
            .ToListAsync();

        foreach (var dest in destinataires)
            await _email.EnvoyerDemandeCreationCompteAuDSIAsync(
                dest, $"{demande.Nom} {demande.Prenoms}", nomActeur,
                demande.Direction?.Libelle ?? "—", demande.Service);

        return WorkflowResult.Success($"Demande de {demande.Nom} {demande.Prenoms} validée. La DSI a été notifiée.");
    }

    public async Task<WorkflowResult> RefuserDmAsync(Guid id, string? commentaire, Guid currentUserId, bool hasFullScope, string nomActeur)
    {
        var demande = await _db.DemandesCreationCompte
            .Include(d => d.DirecteurMetier)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (demande == null) return WorkflowResult.NotFound();

        if (!hasFullScope && demande.DirecteurMetierId != currentUserId)
            return WorkflowResult.Forbidden();

        if (demande.Statut != StatutDemandeCompte.EnAttenteValidationDM)
            return WorkflowResult.Error("Cette demande n'est pas en attente de validation DM.");

        demande.Statut           = StatutDemandeCompte.RefuseeParDM;
        demande.CommentaireDM     = commentaire;
        demande.DateModification = DateTime.Now;
        demande.ModifiePar       = _currentUser.Matricule;
        await _db.SaveChangesAsync();

        await _email.EnvoyerRefusCreationCompteAsync(
            demande.Email, $"{demande.Nom} {demande.Prenoms}", nomActeur, commentaire);

        return WorkflowResult.Success("Demande refusée. L'intéressé a été notifié.");
    }

    public async Task<WorkflowResult> ValiderDsiAsync(Guid id, string? commentaire, RoleUtilisateur role)
    {
        var demande = await _db.DemandesCreationCompte
            .Include(d => d.Direction)
            .Include(d => d.DirecteurMetier)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (demande == null) return WorkflowResult.NotFound();

        if (demande.Statut != StatutDemandeCompte.ValideeParDM)
            return WorkflowResult.Error("Cette demande doit d'abord être validée par le Directeur Métier.");

        var emailExistant = await _db.Utilisateurs
            .AnyAsync(u => u.Email == demande.Email && !u.EstSupprime);
        if (emailExistant)
            return WorkflowResult.Error("Un compte avec cet email existe déjà.");

        var baseMatricule = $"{demande.Prenoms[0]}{demande.Nom}".ToLower().Replace(" ", "").Replace("-", "");
        var matricule = baseMatricule;
        var compteur = 1;
        while (await _db.Utilisateurs.AnyAsync(u => u.Matricule == matricule))
            matricule = $"{baseMatricule}{compteur++}";

        var utilisateur = new Utilisateur
        {
            Id = Guid.NewGuid(), Matricule = matricule, MotDePasse = string.Empty,
            Nom = demande.Nom, Prenoms = demande.Prenoms, Email = demande.Email,
            DirectionId = demande.DirectionId, DateCreation = DateTime.Now,
            CreePar = _currentUser.Matricule ?? "SYSTEM", ModifiePar = string.Empty,
            EstSupprime = false, NombreConnexion = 0
        };
        _db.Utilisateurs.Add(utilisateur);

        _db.UtilisateurRoles.Add(new UtilisateurRole
        {
            Id = Guid.NewGuid(), UtilisateurId = utilisateur.Id, Role = role,
            DateDebut = DateTime.Now, DateCreation = DateTime.Now,
            CreePar = _currentUser.Matricule ?? "SYSTEM", EstSupprime = false
        });

        demande.Statut             = StatutDemandeCompte.CompteCree;
        demande.CommentaireDSI      = commentaire;
        demande.UtilisateurCreePar = utilisateur.Id;
        demande.DateModification   = DateTime.Now;
        demande.ModifiePar         = _currentUser.Matricule;

        var jeton = await _passwordSetupTokenService.CreerAsync(
            utilisateur.Id,
            _currentUser.Matricule ?? "SYSTEM");

        await _db.SaveChangesAsync();

        await _audit.LogActionAsync("CREATION_COMPTE_DSI", DomainEntityTypes.DemandeCreationCompte, demande.Id,
            null, new { MatriculeCreated = matricule, Role = role.ToString() });

        var nomComplet = $"{demande.Nom} {demande.Prenoms}";
        await _email.EnvoyerActivationCompteAsync(
            demande.Email,
            nomComplet,
            matricule,
            BuildActivationLink(utilisateur.Id, jeton.Token),
            jeton.DateExpiration);

        if (demande.DirecteurMetier?.Email != null)
            await _email.EnvoyerConfirmationCreationCompteAuDMAsync(
                demande.DirecteurMetier.Email,
                $"{demande.DirecteurMetier.Nom} {demande.DirecteurMetier.Prenoms}", nomComplet);

        return WorkflowResult.Success($"Compte cree pour {nomComplet}. Un lien d'activation a ete envoye par email.");
    }

    public async Task<WorkflowResult> RefuserDsiAsync(Guid id, string? commentaire, string nomActeur)
    {
        var demande = await _db.DemandesCreationCompte.FindAsync(id);
        if (demande == null) return WorkflowResult.NotFound();

        if (demande.Statut != StatutDemandeCompte.ValideeParDM)
            return WorkflowResult.Error("Cette demande doit d'abord etre validee par le Directeur Metier.");

        demande.Statut           = StatutDemandeCompte.RefuseeParDSI;
        demande.CommentaireDSI    = commentaire;
        demande.DateModification = DateTime.Now;
        demande.ModifiePar       = _currentUser.Matricule;
        await _db.SaveChangesAsync();

        await _email.EnvoyerRefusCreationCompteAsync(
            demande.Email, $"{demande.Nom} {demande.Prenoms}", nomActeur, commentaire);

        return WorkflowResult.Success("Demande refusée par la DSI. L'intéressé a été notifié.");
    }

    private string BuildActivationLink(Guid utilisateurId, string token)
    {
        var baseUrl = (_configuration["SmtpSettings:BaseUrl"] ?? string.Empty).Trim().TrimEnd('/');
        var path = $"/Account/InitialiserMotDePasse?utilisateurId={utilisateurId}&token={Uri.EscapeDataString(token)}";
        return string.IsNullOrWhiteSpace(baseUrl) ? path : $"{baseUrl}{path}";
    }
}
