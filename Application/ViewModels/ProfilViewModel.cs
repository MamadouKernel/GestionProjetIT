using System.ComponentModel.DataAnnotations;

namespace GestionProjects.Application.ViewModels
{
    public class ProfilViewModel
    {
        public Guid Id { get; set; }

        [Display(Name = "Matricule")]
        public string Matricule { get; set; }

        [Required(ErrorMessage = "Le nom est requis")]
        [Display(Name = "Nom")]
        public string Nom { get; set; }

        [Required(ErrorMessage = "Le prénom est requis")]
        [Display(Name = "Prénoms")]
        public string Prenoms { get; set; }

        [Required(ErrorMessage = "L'email est requis")]
        [EmailAddress(ErrorMessage = "L'email n'est pas valide")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Direction")]
        public string? DirectionLibelle { get; set; }

        [Display(Name = "Rôle")]
        public string Role { get; set; }

        [Display(Name = "Changer le mot de passe")]
        public string? NouveauMotDePasse { get; set; }

        [Display(Name = "Confirmer le nouveau mot de passe")]
        [Compare(nameof(NouveauMotDePasse), ErrorMessage = "Les mots de passe ne correspondent pas")]
        public string? ConfirmerMotDePasse { get; set; }

        [Display(Name = "Mot de passe actuel")]
        public string? MotDePasseActuel { get; set; }

        [Display(Name = "Date de dernière connexion")]
        public DateTime? DateDerniereConnexion { get; set; }

        [Display(Name = "Nombre de connexions")]
        public int NombreConnexion { get; set; }
    }
}


