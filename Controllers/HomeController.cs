using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Enums;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;

namespace GestionProjects.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;

        public HomeController(ApplicationDbContext db)
        {
            _db = db;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            // Si pas connecté, rediriger vers login
            if (!User?.Identity?.IsAuthenticated == true)
                return RedirectToAction("Login", "Account");

            var userId = User.GetUserIdOrThrow();
            var userRole = User.GetUserRole();

            // Statistiques selon le rôle
            var stats = new DashboardStatsViewModel();

            if (User.IsInRole("DSI") || User.IsInRole("AdminIT"))
            {
                // Vue globale pour DSI/AdminIT
                stats.TotalProjets = await _db.Projets.CountAsync();
                stats.ProjetsEnCours = await _db.Projets.CountAsync(p => p.StatutProjet == StatutProjet.EnCours);
                stats.DemandesEnAttente = await _db.DemandesProjets.CountAsync(d => d.StatutDemande == StatutDemande.EnAttenteValidationDSI);
                stats.TotalUtilisateurs = await _db.Utilisateurs.CountAsync(u => !u.EstSupprime);
                stats.ProjetsEnAnalyse = await _db.Projets.CountAsync(p => p.PhaseActuelle == PhaseProjet.AnalyseClarification);
                stats.ProjetsEnPlanification = await _db.Projets.CountAsync(p => p.PhaseActuelle == PhaseProjet.PlanificationValidation);
                stats.ProjetsEnExecution = await _db.Projets.CountAsync(p => p.PhaseActuelle == PhaseProjet.ExecutionSuivi);
                stats.ProjetsEnUAT = await _db.Projets.CountAsync(p => p.PhaseActuelle == PhaseProjet.UatMep);
                stats.ProjetsClotures = await _db.Projets.CountAsync(p => p.StatutProjet == StatutProjet.Cloture);
                stats.ProjetsSuspendus = await _db.Projets.CountAsync(p => p.StatutProjet == StatutProjet.Suspendu);
                stats.DemandesValidees = await _db.DemandesProjets.CountAsync(d => d.StatutDemande == StatutDemande.ValideeParDSI);
                stats.DemandesRejetees = await _db.DemandesProjets.CountAsync(d => d.StatutDemande == StatutDemande.RejeteeParDSI);
                
                // Statistiques avancées
                stats.TotalRisques = await _db.Set<Domain.Models.RisqueProjet>().CountAsync();
                stats.RisquesCritiques = await _db.Set<Domain.Models.RisqueProjet>()
                    .CountAsync(r => r.Impact == Domain.Enums.ImpactRisque.Critique);
                stats.TotalAnomalies = await _db.Set<Domain.Models.AnomalieProjet>().CountAsync();
                stats.AnomaliesOuvertes = await _db.Set<Domain.Models.AnomalieProjet>()
                    .CountAsync(a => a.Statut == Domain.Enums.StatutAnomalie.Ouverte);
                stats.TotalLivrables = await _db.Set<Domain.Models.LivrableProjet>().CountAsync();
                
                var projetsAvecAvancement = await _db.Projets
                    .Where(p => p.PourcentageAvancement > 0)
                    .Select(p => p.PourcentageAvancement)
                    .ToListAsync();
                stats.TauxAvancementMoyen = projetsAvecAvancement.Any() 
                    ? projetsAvecAvancement.Average() 
                    : 0;

                // Données pour graphiques - Projets par statut
                stats.ProjetsParStatut = await _db.Projets
                    .GroupBy(p => p.StatutProjet)
                    .ToDictionaryAsync(g => FormatStatutProjet(g.Key), g => g.Count());

                // Données pour graphiques - Demandes par statut
                stats.DemandesParStatut = await _db.DemandesProjets
                    .GroupBy(d => d.StatutDemande)
                    .ToDictionaryAsync(g => FormatStatutDemande(g.Key), g => g.Count());

                // Données pour graphiques - Projets par phase
                stats.ProjetsParPhase = await _db.Projets
                    .GroupBy(p => p.PhaseActuelle)
                    .ToDictionaryAsync(g => FormatPhaseProjet(g.Key), g => g.Count());

                // Évolution des projets sur les 6 derniers mois
                var sixMoisAgo = DateTime.Now.AddMonths(-6);
                var projetsParMois = (await _db.Projets
                    .Where(p => p.DateCreation >= sixMoisAgo)
                    .Select(p => p.DateCreation)
                    .ToListAsync())
                    .GroupBy(p => new { p.Year, p.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .ToList();

                foreach (var groupe in projetsParMois)
                {
                    var date = new DateTime(groupe.Key.Year, groupe.Key.Month, 1);
                    var mois = date.ToString("MMM yyyy", new System.Globalization.CultureInfo("fr-FR"));
                    stats.EvolutionProjets[mois] = groupe.Count();
                }

                // Évolution des demandes sur les 6 derniers mois
                var demandesParMois = (await _db.DemandesProjets
                    .Where(d => d.DateSoumission >= sixMoisAgo)
                    .Select(d => d.DateSoumission)
                    .ToListAsync())
                    .GroupBy(d => new { d.Year, d.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .ToList();

                foreach (var groupe in demandesParMois)
                {
                    var date = new DateTime(groupe.Key.Year, groupe.Key.Month, 1);
                    var mois = date.ToString("MMM yyyy", new System.Globalization.CultureInfo("fr-FR"));
                    stats.EvolutionDemandes[mois] = groupe.Count();
                }

                // Projets par direction
                stats.ProjetsParDirection = await _db.Projets
                    .Include(p => p.Direction)
                    .Where(p => p.Direction != null)
                    .GroupBy(p => p.Direction!.Libelle)
                    .ToDictionaryAsync(g => g.Key, g => g.Count());

                // Projets par urgence (via demande)
                var projetsAvecUrgence = await _db.Projets
                    .Include(p => p.DemandeProjet)
                    .Where(p => p.DemandeProjet != null)
                    .ToListAsync();
                stats.ProjetsParUrgence = projetsAvecUrgence
                    .GroupBy(p => p.DemandeProjet!.Urgence.ToString())
                    .ToDictionary(g => g.Key, g => g.Count());

                // Projets par criticité (via demande)
                stats.ProjetsParCriticite = projetsAvecUrgence
                    .GroupBy(p => p.DemandeProjet!.Criticite.ToString())
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            else if (User.IsInRole("ResponsableSolutionsIT"))
            {
                // Vue pour Responsable Solutions IT (similaire à DSI mais focus sur solutions)
                stats.TotalProjets = await _db.Projets.CountAsync();
                stats.ProjetsEnCours = await _db.Projets.CountAsync(p => p.StatutProjet == StatutProjet.EnCours);
                stats.ProjetsEnAnalyse = await _db.Projets.CountAsync(p => p.PhaseActuelle == PhaseProjet.AnalyseClarification);
                stats.ProjetsEnPlanification = await _db.Projets.CountAsync(p => p.PhaseActuelle == PhaseProjet.PlanificationValidation);
                stats.ProjetsEnExecution = await _db.Projets.CountAsync(p => p.PhaseActuelle == PhaseProjet.ExecutionSuivi);
                stats.ProjetsEnUAT = await _db.Projets.CountAsync(p => p.PhaseActuelle == PhaseProjet.UatMep);
                stats.ProjetsClotures = await _db.Projets.CountAsync(p => p.StatutProjet == StatutProjet.Cloture);
                
                // Statistiques avancées
                stats.TotalRisques = await _db.Set<Domain.Models.RisqueProjet>().CountAsync();
                stats.RisquesCritiques = await _db.Set<Domain.Models.RisqueProjet>()
                    .CountAsync(r => r.Impact == Domain.Enums.ImpactRisque.Critique);
                stats.TotalAnomalies = await _db.Set<Domain.Models.AnomalieProjet>().CountAsync();
                stats.AnomaliesOuvertes = await _db.Set<Domain.Models.AnomalieProjet>()
                    .CountAsync(a => a.Statut == Domain.Enums.StatutAnomalie.Ouverte);
                stats.TotalLivrables = await _db.Set<Domain.Models.LivrableProjet>().CountAsync();
                
                var projetsAvecAvancement = await _db.Projets
                    .Where(p => p.PourcentageAvancement > 0)
                    .Select(p => p.PourcentageAvancement)
                    .ToListAsync();
                stats.TauxAvancementMoyen = projetsAvecAvancement.Any() 
                    ? projetsAvecAvancement.Average() 
                    : 0;

                // Données pour graphiques
                stats.ProjetsParStatut = await _db.Projets
                    .GroupBy(p => p.StatutProjet)
                    .ToDictionaryAsync(g => FormatStatutProjet(g.Key), g => g.Count());

                stats.ProjetsParPhase = await _db.Projets
                    .GroupBy(p => p.PhaseActuelle)
                    .ToDictionaryAsync(g => FormatPhaseProjet(g.Key), g => g.Count());

                // Évolution des projets
                var sixMoisAgo = DateTime.Now.AddMonths(-6);
                var projetsParMois = (await _db.Projets
                    .Where(p => p.DateCreation >= sixMoisAgo)
                    .Select(p => p.DateCreation)
                    .ToListAsync())
                    .GroupBy(p => new { p.Year, p.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .ToList();

                foreach (var groupe in projetsParMois)
                {
                    var date = new DateTime(groupe.Key.Year, groupe.Key.Month, 1);
                    var mois = date.ToString("MMM yyyy", new System.Globalization.CultureInfo("fr-FR"));
                    stats.EvolutionProjets[mois] = groupe.Count();
                }

                // Projets par direction
                stats.ProjetsParDirection = await _db.Projets
                    .Include(p => p.Direction)
                    .Where(p => p.Direction != null)
                    .GroupBy(p => p.Direction!.Libelle)
                    .ToDictionaryAsync(g => g.Key, g => g.Count());
            }
            else if (User.IsInRole("DirecteurMetier"))
            {
                // Vue pour Directeur Métier - Filtrer les projets où il est sponsor OU les projets de sa direction
                var user = await _db.Utilisateurs
                    .Include(u => u.Direction)
                    .FirstOrDefaultAsync(u => u.Id == userId);
                var directionId = user?.DirectionId;
                
                stats.DemandesEnAttente = await _db.DemandesProjets.CountAsync(d => 
                    (d.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier ||
                     d.StatutDemande == StatutDemande.RetourneeAuDirecteurMetierParDSI) &&
                    d.DirecteurMetierId == userId);
                
                stats.DemandesValidees = await _db.DemandesProjets.CountAsync(d => 
                    d.DirecteurMetierId == userId && 
                    d.StatutDemande == StatutDemande.EnAttenteValidationDSI);
                
                stats.DemandesRejetees = await _db.DemandesProjets.CountAsync(d => 
                    d.DirecteurMetierId == userId && 
                    d.StatutDemande == StatutDemande.RejeteeParDirecteurMetier);
                
                // Filtrer les projets : où il est sponsor OU de sa direction
                IQueryable<Domain.Models.Projet> projetsQuery = _db.Projets.Where(p => p.SponsorId == userId);
                if (directionId.HasValue)
                {
                    projetsQuery = _db.Projets.Where(p => p.SponsorId == userId || p.DirectionId == directionId);
                }
                
                stats.TotalProjets = await projetsQuery.CountAsync();
                stats.ProjetsEnCours = await projetsQuery.CountAsync(p => p.StatutProjet == StatutProjet.EnCours);
                stats.ProjetsEnAnalyse = await projetsQuery.CountAsync(p => p.PhaseActuelle == PhaseProjet.AnalyseClarification);
                stats.ProjetsEnPlanification = await projetsQuery.CountAsync(p => p.PhaseActuelle == PhaseProjet.PlanificationValidation);
                stats.ProjetsEnExecution = await projetsQuery.CountAsync(p => p.PhaseActuelle == PhaseProjet.ExecutionSuivi);
                stats.ProjetsClotures = await projetsQuery.CountAsync(p => p.StatutProjet == StatutProjet.Cloture);
                
                // Statistiques avancées
                stats.TotalRisques = await _db.Set<Domain.Models.RisqueProjet>()
                    .Include(r => r.Projet)
                    .CountAsync(r => projetsQuery.Any(p => p.Id == r.ProjetId));
                stats.TotalAnomalies = await _db.Set<Domain.Models.AnomalieProjet>()
                    .Include(a => a.Projet)
                    .CountAsync(a => projetsQuery.Any(p => p.Id == a.ProjetId));
                stats.AnomaliesOuvertes = await _db.Set<Domain.Models.AnomalieProjet>()
                    .Include(a => a.Projet)
                    .CountAsync(a => projetsQuery.Any(p => p.Id == a.ProjetId) && a.Statut == Domain.Enums.StatutAnomalie.Ouverte);
                
                var projetsAvecAvancement = await projetsQuery
                    .Where(p => p.PourcentageAvancement > 0)
                    .Select(p => p.PourcentageAvancement)
                    .ToListAsync();
                stats.TauxAvancementMoyen = projetsAvecAvancement.Any() 
                    ? projetsAvecAvancement.Average() 
                    : 0;
                
                // Projets par statut pour ce Directeur Métier (ses projets + projets de sa direction)
                stats.ProjetsParStatut = await projetsQuery
                    .GroupBy(p => p.StatutProjet)
                    .ToDictionaryAsync(g => FormatStatutProjet(g.Key), g => g.Count());

                // Projets par phase pour ce Directeur Métier
                stats.ProjetsParPhase = await projetsQuery
                    .GroupBy(p => p.PhaseActuelle)
                    .ToDictionaryAsync(g => FormatPhaseProjet(g.Key), g => g.Count());

                // Évolution des projets de ce Directeur Métier
                var sixMoisAgo = DateTime.Now.AddMonths(-6);
                var projetsParMois = (await projetsQuery
                    .Where(p => p.DateCreation >= sixMoisAgo)
                    .Select(p => p.DateCreation)
                    .ToListAsync())
                    .GroupBy(p => new { p.Year, p.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .ToList();

                foreach (var groupe in projetsParMois)
                {
                    var date = new DateTime(groupe.Key.Year, groupe.Key.Month, 1);
                    var mois = date.ToString("MMM yyyy", new System.Globalization.CultureInfo("fr-FR"));
                    stats.EvolutionProjets[mois] = groupe.Count();
                }

                // Demandes par statut pour ce DM
                stats.DemandesParStatut = await _db.DemandesProjets
                    .Where(d => d.DirecteurMetierId == userId)
                    .GroupBy(d => d.StatutDemande)
                    .ToDictionaryAsync(g => FormatStatutDemande(g.Key), g => g.Count());

                // Évolution des demandes
                var sixMoisAgoDM = DateTime.Now.AddMonths(-6);
                var demandesParMoisDM = (await _db.DemandesProjets
                    .Where(d => d.DirecteurMetierId == userId && d.DateSoumission >= sixMoisAgoDM)
                    .Select(d => d.DateSoumission)
                    .ToListAsync())
                    .GroupBy(d => new { d.Year, d.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .ToList();

                foreach (var groupe in demandesParMoisDM)
                {
                    var date = new DateTime(groupe.Key.Year, groupe.Key.Month, 1);
                    var mois = date.ToString("MMM yyyy", new System.Globalization.CultureInfo("fr-FR"));
                    stats.EvolutionDemandes[mois] = groupe.Count();
                }
            }
            else if (User.IsInRole("ChefDeProjet"))
            {
                // Vue pour Chef de Projet
                stats.TotalProjets = await _db.Projets.CountAsync(p => p.ChefProjetId == userId);
                stats.ProjetsEnCours = await _db.Projets.CountAsync(p => p.ChefProjetId == userId && p.StatutProjet == StatutProjet.EnCours);
                stats.ProjetsEnAnalyse = await _db.Projets.CountAsync(p => p.ChefProjetId == userId && p.PhaseActuelle == PhaseProjet.AnalyseClarification);
                stats.ProjetsEnPlanification = await _db.Projets.CountAsync(p => p.ChefProjetId == userId && p.PhaseActuelle == PhaseProjet.PlanificationValidation);
                stats.ProjetsEnExecution = await _db.Projets.CountAsync(p => p.ChefProjetId == userId && p.PhaseActuelle == PhaseProjet.ExecutionSuivi);
                stats.ProjetsEnUAT = await _db.Projets.CountAsync(p => p.ChefProjetId == userId && p.PhaseActuelle == PhaseProjet.UatMep);
                stats.ProjetsClotures = await _db.Projets.CountAsync(p => p.ChefProjetId == userId && p.StatutProjet == StatutProjet.Cloture);
                stats.ProjetsSuspendus = await _db.Projets.CountAsync(p => p.ChefProjetId == userId && p.StatutProjet == StatutProjet.Suspendu);

                // Statistiques avancées
                stats.TotalRisques = await _db.Set<Domain.Models.RisqueProjet>()
                    .CountAsync(r => r.Projet.ChefProjetId == userId);
                stats.RisquesCritiques = await _db.Set<Domain.Models.RisqueProjet>()
                    .CountAsync(r => r.Projet.ChefProjetId == userId && r.Impact == Domain.Enums.ImpactRisque.Critique);
                stats.TotalAnomalies = await _db.Set<Domain.Models.AnomalieProjet>()
                    .CountAsync(a => a.Projet.ChefProjetId == userId);
                stats.AnomaliesOuvertes = await _db.Set<Domain.Models.AnomalieProjet>()
                    .CountAsync(a => a.Projet.ChefProjetId == userId && a.Statut == Domain.Enums.StatutAnomalie.Ouverte);
                stats.TotalLivrables = await _db.Set<Domain.Models.LivrableProjet>()
                    .CountAsync(l => l.Projet.ChefProjetId == userId);
                
                var projetsAvecAvancement = await _db.Projets
                    .Where(p => p.ChefProjetId == userId && p.PourcentageAvancement > 0)
                    .Select(p => p.PourcentageAvancement)
                    .ToListAsync();
                stats.TauxAvancementMoyen = projetsAvecAvancement.Any() 
                    ? projetsAvecAvancement.Average() 
                    : 0;

                // Projets par statut
                stats.ProjetsParStatut = await _db.Projets
                    .Where(p => p.ChefProjetId == userId)
                    .GroupBy(p => p.StatutProjet)
                    .ToDictionaryAsync(g => FormatStatutProjet(g.Key), g => g.Count());

                // Projets par phase
                stats.ProjetsParPhase = await _db.Projets
                    .Where(p => p.ChefProjetId == userId)
                    .GroupBy(p => p.PhaseActuelle)
                    .ToDictionaryAsync(g => FormatPhaseProjet(g.Key), g => g.Count());

                // Évolution des projets
                var sixMoisAgoCP = DateTime.Now.AddMonths(-6);
                var projetsParMoisCP = (await _db.Projets
                    .Where(p => p.ChefProjetId == userId && p.DateCreation >= sixMoisAgoCP)
                    .Select(p => p.DateCreation)
                    .ToListAsync())
                    .GroupBy(p => new { p.Year, p.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .ToList();

                foreach (var groupe in projetsParMoisCP)
                {
                    var date = new DateTime(groupe.Key.Year, groupe.Key.Month, 1);
                    var mois = date.ToString("MMM yyyy", new System.Globalization.CultureInfo("fr-FR"));
                    stats.EvolutionProjets[mois] = groupe.Count();
                }
            }
            else if (User.IsInRole("Demandeur"))
            {
                // Vue pour Demandeur
                stats.TotalDemandes = await _db.DemandesProjets.CountAsync(d => d.DemandeurId == userId);
                stats.DemandesEnAttente = await _db.DemandesProjets.CountAsync(d => d.DemandeurId == userId && 
                    (d.StatutDemande == StatutDemande.EnAttenteValidationDirecteurMetier ||
                     d.StatutDemande == StatutDemande.EnAttenteValidationDSI));
                stats.DemandesValidees = await _db.DemandesProjets.CountAsync(d => d.DemandeurId == userId && d.StatutDemande == StatutDemande.ValideeParDSI);
                stats.DemandesRejetees = await _db.DemandesProjets.CountAsync(d => d.DemandeurId == userId && 
                    (d.StatutDemande == StatutDemande.RejeteeParDirecteurMetier || 
                     d.StatutDemande == StatutDemande.RejeteeParDSI));
                stats.TotalProjets = await _db.Projets.CountAsync(p => p.DemandeProjet != null && p.DemandeProjet.DemandeurId == userId);
                stats.ProjetsEnCours = await _db.Projets.CountAsync(p => 
                    p.DemandeProjet != null && 
                    p.DemandeProjet.DemandeurId == userId && 
                    p.StatutProjet == StatutProjet.EnCours);

                // Demandes par statut
                stats.DemandesParStatut = await _db.DemandesProjets
                    .Where(d => d.DemandeurId == userId)
                    .GroupBy(d => d.StatutDemande)
                    .ToDictionaryAsync(g => FormatStatutDemande(g.Key), g => g.Count());

                // Évolution des demandes sur les 6 derniers mois
                var sixMoisAgo = DateTime.Now.AddMonths(-6);
                var demandesParMois = (await _db.DemandesProjets
                    .Where(d => d.DemandeurId == userId && d.DateSoumission >= sixMoisAgo)
                    .Select(d => d.DateSoumission)
                    .ToListAsync())
                    .GroupBy(d => new { d.Year, d.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .ToList();

                foreach (var groupe in demandesParMois)
                {
                    var date = new DateTime(groupe.Key.Year, groupe.Key.Month, 1);
                    var mois = date.ToString("MMM yyyy", new System.Globalization.CultureInfo("fr-FR"));
                    stats.EvolutionDemandes[mois] = groupe.Count();
                }
            }

            return View(stats);
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var requestId = HttpContext.TraceIdentifier;
            return View(new ErrorViewModel { RequestId = requestId });
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
    }
}

