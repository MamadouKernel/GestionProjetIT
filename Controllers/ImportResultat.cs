namespace GestionProjects.Controllers
{
    public class ImportResultat
    {
        public int Ligne { get; set; }
        public string Matricule { get; set; } = "";
        public string Nom { get; set; } = "";
        public string Statut { get; set; } = ""; // "Créé", "Ignoré", "Erreur"
        public string Message { get; set; } = "";
    }
}

