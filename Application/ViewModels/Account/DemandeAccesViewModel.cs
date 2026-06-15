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

    public sealed record DirectionOption(Guid Id, string Libelle);
}
