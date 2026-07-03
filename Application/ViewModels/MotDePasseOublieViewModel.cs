using System.ComponentModel.DataAnnotations;

namespace GestionProjects.Application.ViewModels
{
    public class MotDePasseOublieViewModel
    {
        [Required(ErrorMessage = "Le matricule est requis")]
        [Display(Name = "Matricule")]
        public string Matricule { get; set; } = string.Empty;

        [Required(ErrorMessage = "L'email est requis")]
        [EmailAddress(ErrorMessage = "Adresse email invalide")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }
}
