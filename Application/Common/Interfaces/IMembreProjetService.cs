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

        /// <summary>Retire (soft-delete) un membre. False si introuvable ou n'appartenant pas au projet.</summary>
        Task<bool> RetirerMembreAsync(Guid projetId, Guid membreId);
    }
}
