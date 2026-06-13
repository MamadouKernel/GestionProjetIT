using System.ComponentModel.DataAnnotations;

namespace GestionProjects.Application.ViewModels;

public class InitialiserMotDePasseViewModel
{
    [Required]
    public Guid UtilisateurId { get; set; }

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nouveau mot de passe est requis.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nouveau mot de passe")]
    public string NouveauMotDePasse { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmation du mot de passe est requise.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmer le mot de passe")]
    [Compare(nameof(NouveauMotDePasse), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    public string ConfirmerMotDePasse { get; set; } = string.Empty;
}
