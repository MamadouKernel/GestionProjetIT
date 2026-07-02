using GestionProjects.Domain.Enums;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Extensions;

/// <summary>
/// Enrichit l'audit des actions "Chef de Projet" (livrables, charges, bénéfices, avenants...)
/// avec la traçabilité exigée par l'évolution "délégation des rôles" : si l'auteur réel n'est
/// pas le Chef de Projet affecté, l'action est suffixée et le détail précise s'il agit au titre
/// d'une délégation (DelegationChefProjet, DSI → délégué) ou comme Responsable Solution IT
/// (qui n'a besoin d'aucune délégation formelle pour remplacer le Chef de Projet affecté).
/// </summary>
public static class ChefProjetAuditContextExtensions
{
    public static async Task<(string TypeAction, object? Details)> BuildChefProjetAuditAsync(
        this ApplicationDbContext db,
        string baseTypeAction,
        Guid projetId,
        Guid agissantId,
        object? valeurs = null)
    {
        var chefProjetId = await db.Projets
            .Where(p => p.Id == projetId)
            .Select(p => p.ChefProjetId)
            .FirstOrDefaultAsync();

        if (chefProjetId.HasValue && chefProjetId.Value == agissantId)
            return (baseTypeAction, valeurs);

        var delegantId = await db.DelegationsChefProjet
            .Where(d => d.ProjetId == projetId && d.DelegueId == agissantId && d.EstActive &&
                        d.DateDebut <= DateTime.Now && (d.DateFin == null || d.DateFin >= DateTime.Now) &&
                        !d.EstSupprime)
            .OrderByDescending(d => d.DateDebut)
            .Select(d => (Guid?)d.DelegantId)
            .FirstOrDefaultAsync();

        if (delegantId.HasValue)
        {
            var titulaire = await db.Utilisateurs.FindAsync(delegantId.Value);
            var nomTitulaire = titulaire != null ? $"{titulaire.Nom} {titulaire.Prenoms}".Trim() : null;
            return ($"{baseTypeAction}_PAR_DELEGUE_CHEFPROJET", new { TitulaireInitialDuRole = nomTitulaire, Details = valeurs });
        }

        if (chefProjetId.HasValue)
        {
            var estResponsableSolutionIT = await db.UtilisateurRoles.AnyAsync(ur =>
                ur.UtilisateurId == agissantId && !ur.EstSupprime && ur.Role == RoleUtilisateur.ResponsableSolutionsIT);

            if (estResponsableSolutionIT)
            {
                var chefProjet = await db.Utilisateurs.FindAsync(chefProjetId.Value);
                var nomChefProjet = chefProjet != null ? $"{chefProjet.Nom} {chefProjet.Prenoms}".Trim() : null;
                return ($"{baseTypeAction}_REMPLACEMENT_CHEFPROJET", new { ChefProjetAffecte = nomChefProjet, Details = valeurs });
            }
        }

        return (baseTypeAction, valeurs);
    }
}
