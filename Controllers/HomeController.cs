using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Enums;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace GestionProjects.Controllers
{
    public partial class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IPermissionService _permissionService;

        public HomeController(ApplicationDbContext db, IPermissionService permissionService)
        {
            _db = db;
            _permissionService = permissionService;
        }

        private string FormatStatutProjet(StatutProjet statut)
        {
            return statut switch
            {
                StatutProjet.NonDemarre => "Non Démarré",
                StatutProjet.EnCours => "En Cours",
                StatutProjet.Suspendu => "Suspendu",
                StatutProjet.ClotureEnCours => "Clôture en Cours",
                StatutProjet.Cloture => "Clôturé",
                StatutProjet.Annule => "Annulé",
                _ => statut.ToString()
            };
        }

        private string FormatStatutDemande(StatutDemande statut)
        {
            return statut switch
            {
                StatutDemande.Brouillon => "Brouillon",
                StatutDemande.EnAttenteValidationDirecteurMetier => "En Attente DM",
                StatutDemande.CorrectionDemandeeParDirecteurMetier => "Correction Demandée",
                StatutDemande.RejeteeParDirecteurMetier => "Rejetée par DM",
                StatutDemande.EnAttenteValidationDSI => "En Attente DSI",
                StatutDemande.RetourneeAuDemandeurParDSI => "Retour Demandeur",
                StatutDemande.RetourneeAuDirecteurMetierParDSI => "Retour DM",
                StatutDemande.RejeteeParDSI => "Rejetée par DSI",
                StatutDemande.ValideeParDSI => "Validée",
                _ => statut.ToString()
            };
        }

        private string FormatPhaseProjet(PhaseProjet phase)
        {
            return phase switch
            {
                PhaseProjet.Demande => "Demande",
                PhaseProjet.AnalyseClarification => "Analyse & Clarification",
                PhaseProjet.PlanificationValidation => "Planification",
                PhaseProjet.ExecutionSuivi => "Exécution",
                PhaseProjet.UatMep => "UAT & MEP",
                PhaseProjet.ClotureLeconsApprises => "Clôture",
                _ => phase.ToString()
            };
        }

        private string FormatEtatProjet(EtatProjet etat)
        {
            return etat switch
            {
                EtatProjet.Vert => "Vert",
                EtatProjet.Orange => "Orange",
                EtatProjet.Rouge => "Rouge",
                _ => etat.ToString()
            };
        }

        private string FormatProfilRessource(ProfilRessource? profil)
        {
            return profil switch
            {
                ProfilRessource.Developpement => "Développement",
                ProfilRessource.Infrastructure => "Infrastructure",
                ProfilRessource.Support => "Support",
                ProfilRessource.DBA => "DBA",
                ProfilRessource.ChefProjet => "Chefferie projet",
                ProfilRessource.Architecte => "Architecture",
                ProfilRessource.Analyste => "Analyse",
                ProfilRessource.Autre => "Autres",
                _ => "Non défini"
            };
        }

        private string FormatUtilisateurCourt(string? nom, string? prenoms)
        {
            if (string.IsNullOrWhiteSpace(nom) && string.IsNullOrWhiteSpace(prenoms))
            {
                return "Non affecté";
            }

            if (string.IsNullOrWhiteSpace(prenoms))
            {
                return nom ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(nom))
            {
                return prenoms ?? string.Empty;
            }

            return $"{nom} {prenoms}";
        }

        private string FormatMontantCompact(decimal? montant)
        {
            if (!montant.HasValue || montant.Value <= 0)
            {
                return "0,0 M";
            }

            return $"{Math.Round(montant.Value / 1_000_000m, 1):N1} M";
        }
    }
}
