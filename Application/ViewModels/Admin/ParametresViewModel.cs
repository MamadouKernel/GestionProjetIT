using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Admin
{
    public class ParametresViewModel
    {
        public List<ParametreSysteme> Parametres { get; set; } = new();
        public List<Utilisateur> UtilisateursDsi { get; set; } = new();
        public string? DSIPrincipalId { get; set; }
        public string? DSIDelegueId { get; set; }
        public string? DelaiInactiviteSessionMinutes { get; set; }
        public string? RepertoireStockageRacine { get; set; }
        public string? TypesLivrables { get; set; }
    }
}
