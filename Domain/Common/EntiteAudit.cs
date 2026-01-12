namespace GestionProjects.Domain.Common
{
    public abstract class EntiteAudit
    {
        public DateTime DateCreation { get; set; }
        public string CreePar { get; set; } = string.Empty;
        public DateTime? DateModification { get; set; }
        public string? ModifiePar { get; set; }
        public bool EstSupprime { get; set; } = false;
    }
}
