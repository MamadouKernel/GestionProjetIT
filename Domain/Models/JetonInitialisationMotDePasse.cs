using GestionProjects.Domain.Common;

namespace GestionProjects.Domain.Models;

public class JetonInitialisationMotDePasse : EntiteAudit
{
    public Guid Id { get; set; }
    public Guid UtilisateurId { get; set; }
    public Utilisateur Utilisateur { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime DateExpiration { get; set; }
    public DateTime? DateUtilisation { get; set; }
    public string? UtiliseDepuisIp { get; set; }
}
