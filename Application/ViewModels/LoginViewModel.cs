using System.ComponentModel.DataAnnotations;

namespace GestionProjects.Application.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Le matricule est requis")]
        [Display(Name = "Matricule")]
        public string Matricule { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le mot de passe est requis")]
        [DataType(DataType.Password)]
        [Display(Name = "Mot de passe")]
        public string MotDePasse { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }
}
