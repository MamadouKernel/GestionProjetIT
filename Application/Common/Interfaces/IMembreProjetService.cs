namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Gestion des membres d'un projet (ajout, retrait logique). Le contrôleur garde
    /// l'autorisation ; ce service possède la persistance + l'audit.
    /// </summary>
    public interface IMembreProjetService
    {
        /// <summary>Ajoute un membre depuis un utilisateur existant. False si l'utilisateur est introuvable.</summary>
        Task<bool> AjouterMembreAsync(Guid projetId, Guid utilisateurId, string roleDansProjet);

        /// <summary>Crée un membre externe/hors-système saisi en texte libre (pas d'utilisateur applicatif associé).</summary>
        Task<Guid> CreerMembreAsync(Guid projetId, string nom, string prenom, string email, string roleDansProjet, string? fonction, string? directionLibelle);

        /// <summary>Met à jour les champs d'un membre existant. False si introuvable ou n'appartenant pas au projet.</summary>
        Task<bool> ModifierMembreAsync(Guid projetId, Guid membreId, string nom, string prenom, string email, string roleDansProjet, string? directionLibelle);

        /// <summary>Retire (soft-delete) un membre. False si introuvable ou n'appartenant pas au projet.</summary>
        Task<bool> RetirerMembreAsync(Guid projetId, Guid membreId);
    }
}
