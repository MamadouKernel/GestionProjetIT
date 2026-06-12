namespace GestionProjects.Application.ViewModels.AzureAuth
{
    public class DemanderAccesViewModel
    {
        public string Email { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string Matricule { get; set; } = string.Empty;
        public string AzureDepartment { get; set; } = string.Empty;
        public string DirectionDetecteeId { get; set; } = string.Empty;
        public string DirectionDetecteeNom { get; set; } = "Non déterminée";
    }
}
