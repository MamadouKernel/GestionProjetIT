using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public sealed class DemandeCreationCompteWorkflowService : IDemandeCreationCompteWorkflowService
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailService _email;

    public DemandeCreationCompteWorkflowService(
        ApplicationDbContext db,
        IEmailService email)
    {
        _db = db;
        _email = email;
    }

    public async Task<WorkflowResult> SoumettreAsync(SoumettreDemandeCreationCompteInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Nom) ||
            string.IsNullOrWhiteSpace(input.Prenoms) ||
            string.IsNullOrWhiteSpace(input.Email) ||
            input.DirectionId is null ||
            input.DirecteurMetierId is null)
        {
            return WorkflowResult.Error("Tous les champs obligatoires doivent etre remplis.");
        }

        var nomNormalise = input.Nom.Trim();
        var prenomsNormalise = input.Prenoms.Trim();
        var emailNormalise = input.Email.Trim();
        var serviceNormalise = input.ServiceLibelle?.Trim() ?? string.Empty;
        var emailLookup = emailNormalise.ToUpperInvariant();

        var emailDejaUtilise = await _db.Utilisateurs
            .AnyAsync(u => !u.EstSupprime && u.Email.ToUpper() == emailLookup);
        if (emailDejaUtilise)
        {
            return WorkflowResult.Error("Un compte avec cette adresse email existe deja.");
        }

        var demandeDejaEnCours = await _db.DemandesCreationCompte
            .AnyAsync(d =>
                !d.EstSupprime &&
                d.Email.ToUpper() == emailLookup &&
                d.Statut != StatutDemandeCompte.RefuseeParDM &&
                d.Statut != StatutDemandeCompte.RefuseeParDSI);
        if (demandeDejaEnCours)
        {
            return WorkflowResult.Error("Une demande de creation de compte est deja en cours pour cette adresse email.");
        }

        var direction = await _db.Directions
            .FirstOrDefaultAsync(d =>
                d.Id == input.DirectionId.Value &&
                !d.EstSupprime &&
                d.EstActive);
        if (direction == null)
        {
            return WorkflowResult.Error("La direction selectionnee est invalide.");
        }

        var directeurMetier = await _db.Utilisateurs
            .Include(u => u.UtilisateurRoles)
            .FirstOrDefaultAsync(u =>
                u.Id == input.DirecteurMetierId.Value &&
                !u.EstSupprime &&
                u.DirectionId == direction.Id);
        if (directeurMetier == null || !HasActiveRole(directeurMetier, RoleUtilisateur.DirecteurMetier))
        {
            return WorkflowResult.Error("Le Directeur Metier selectionne est invalide pour cette direction.");
        }

        var demande = new DemandeCreationCompte
        {
            Id = Guid.NewGuid(),
            Nom = nomNormalise,
            Prenoms = prenomsNormalise,
            Email = emailNormalise,
            Service = serviceNormalise,
            DirectionId = direction.Id,
            DirecteurMetierId = directeurMetier.Id,
            Statut = StatutDemandeCompte.EnAttenteValidationDM,
            DateSoumission = DateTime.Now,
            DateCreation = DateTime.Now,
            CreePar = "ANONYMOUS",
            EstSupprime = false
        };

        _db.DemandesCreationCompte.Add(demande);
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(directeurMetier.Email))
        {
            await _email.EnvoyerDemandeCreationCompteAuDMAsync(
                directeurMetier.Email,
                $"{directeurMetier.Nom} {directeurMetier.Prenoms}".Trim(),
                $"{nomNormalise} {prenomsNormalise}",
                direction.Libelle,
                string.IsNullOrWhiteSpace(serviceNormalise) ? "-" : serviceNormalise,
                emailNormalise);
        }

        return WorkflowResult.Success(
            "Votre demande a ete transmise a votre Directeur Metier. Vous serez contacte par email une fois votre compte cree.");
    }

    private static bool HasActiveRole(Utilisateur utilisateur, RoleUtilisateur role)
    {
        var now = DateTime.Now;
        return utilisateur.UtilisateurRoles.Any(ur =>
            !ur.EstSupprime &&
            ur.Role == role &&
            (!ur.DateDebut.HasValue || ur.DateDebut.Value <= now) &&
            (!ur.DateFin.HasValue || ur.DateFin.Value >= now));
    }
}
