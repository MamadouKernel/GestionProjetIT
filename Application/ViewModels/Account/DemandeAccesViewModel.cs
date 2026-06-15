namespace GestionProjects.Application.ViewModels.Account
{
    /// <summary>
    /// View-model du formulaire public de demande d'accès local CIT.
    /// Sert principalement à transporter la liste des directions selectionnables.
    /// </summary>
    public class DemandeAccesViewModel
    {
        public List<DirectionOption> Directions { get; set; } = new();
    }

    /// <summary>
    /// Option de direction présentée dans le formulaire. <see cref="ADmActif"/> permet
    /// au front d'avertir l'utilisateur (et au back de bloquer) si la direction n'a
    /// aucun Directeur Métier rattaché : sans DM, le workflow approbation→DM n'a
    /// personne à solliciter.
    /// </summary>
    public sealed record DirectionOption(Guid Id, string Libelle, bool ADmActif);
}
