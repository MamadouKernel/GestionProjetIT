using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.ViewModels;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    public class ChargeProjetService : IChargeProjetService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;

        public ChargeProjetService(
            ApplicationDbContext db,
            IAuditService auditService,
            ICurrentUserService currentUserService)
        {
            _db = db;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// Assemble le view-model de la page Charges (grille hebdomadaire, synthèses, alertes).
        /// Le projet doit être chargé avec Direction/Sponsor/ChefProjet/FicheProjet/Membres/Charges.Ressource.
        /// L'autorisation reste au contrôleur (isPilotage / isProjectMember passés en paramètres).
        /// </summary>
        public async Task<ProjetChargesViewModel> BuildChargesViewModelAsync(
            Projet projet, Guid currentUserId, bool isPilotage, bool isProjectMember)
        {
            var ressources = new List<Utilisateur>();

            if (projet.ChefProjetId.HasValue)
            {
                var chefProjet = await _db.Utilisateurs.FindAsync(projet.ChefProjetId.Value);
                if (chefProjet != null && !chefProjet.EstSupprime)
                    ressources.Add(chefProjet);
            }

            var emailsMembres = projet.Membres
                .Where(m => !m.EstSupprime && !string.IsNullOrWhiteSpace(m.Email))
                .Select(m => m.Email)
                .Distinct()
                .ToList();

            if (emailsMembres.Any())
            {
                var utilisateursParEmail = await _db.Utilisateurs
                    .Where(u => !u.EstSupprime && emailsMembres.Contains(u.Email))
                    .ToListAsync();

                foreach (var membre in utilisateursParEmail)
                {
                    if (!ressources.Any(r => r.Id == membre.Id))
                        ressources.Add(membre);
                }
            }

            foreach (var chargeRessource in projet.Charges
                         .Where(c => !c.EstSupprime && c.Ressource != null)
                         .Select(c => c.Ressource!)
                         .DistinctBy(r => r.Id))
            {
                if (!ressources.Any(r => r.Id == chargeRessource.Id))
                    ressources.Add(chargeRessource);
            }

            ressources = ressources
                .OrderBy(r => r.Nom)
                .ThenBy(r => r.Prenoms)
                .ToList();

            var currentWeek = NormalizeToMonday(DateTime.Now);
            var semaines = Enumerable.Range(-2, 6)
                .Select(offset => currentWeek.AddDays(offset * 7))
                .ToList();

            var activeCharges = projet.Charges
                .Where(c => !c.EstSupprime)
                .ToList();

            var weekModels = semaines
                .Select(week => new ProjetChargesWeekViewModel
                {
                    StartDate = week,
                    Label = $"S{System.Globalization.ISOWeek.GetWeekOfYear(week)}",
                    Subtitle = week.ToString("dd MMM", new System.Globalization.CultureInfo("fr-FR")),
                    IsCurrent = week == currentWeek,
                    IsPast = week < currentWeek,
                    IsFuture = week > currentWeek
                })
                .ToList();

            var canEditForecast = isPilotage;
            var canEditActual = isPilotage || isProjectMember;
            var canValidateCharges = isPilotage;
            var canExport = isPilotage;

            var resourceRows = new List<ProjetChargesResourceRowViewModel>();
            foreach (var ressource in ressources)
            {
                var isCurrentResource = currentUserId == ressource.Id;
                var row = new ProjetChargesResourceRowViewModel
                {
                    ResourceId = ressource.Id,
                    FullName = $"{ressource.Nom} {ressource.Prenoms}".Trim(),
                    Email = ressource.Email,
                    RoleLabel = GetProfilRessourceLabel(ressource.ProfilRessource),
                    WeeklyCapacity = ressource.CapaciteHebdomadaire,
                    CapacityTotal = ressource.CapaciteHebdomadaire * weekModels.Count
                };

                foreach (var week in weekModels)
                {
                    var charge = activeCharges.FirstOrDefault(c =>
                        c.RessourceId == ressource.Id &&
                        NormalizeToMonday(c.SemaineDebut) == week.StartDate);

                    var planned = charge?.ChargePrevisionnelle ?? 0m;
                    var actual = charge?.ChargeReelle;
                    var loadReference = actual ?? planned;
                    var utilization = ressource.CapaciteHebdomadaire > 0
                        ? Math.Round((double)(loadReference / ressource.CapaciteHebdomadaire) * 100, 1)
                        : 0;
                    var allocation = ressource.CapaciteHebdomadaire > 0
                        ? Math.Round(planned / ressource.CapaciteHebdomadaire * 100, 1)
                        : 0;

                    var (status, statusClass) = GetCapacityStatus(utilization);

                    row.Cells.Add(new ProjetChargeCellViewModel
                    {
                        ResourceId = ressource.Id,
                        WeekStart = week.StartDate,
                        PlannedHours = planned,
                        ActualHours = actual,
                        VarianceHours = actual.HasValue ? actual.Value - planned : 0,
                        AllocationPercentage = allocation,
                        Comment = charge?.Commentaire ?? string.Empty,
                        TypeActivite = charge?.TypeActivite ?? string.Empty,
                        Activite = charge?.Activite ?? string.Empty,
                        ValidationComment = charge?.CommentaireValidation ?? string.Empty,
                        ValidationStatus = charge?.StatutValidation ?? StatutValidationCharge.Brouillon,
                        ValidationStatusLabel = GetValidationChargeLabel(charge?.StatutValidation ?? StatutValidationCharge.Brouillon),
                        ValidationStatusClass = GetValidationChargeClass(charge?.StatutValidation ?? StatutValidationCharge.Brouillon),
                        UtilizationRate = utilization,
                        CapacityStatus = status,
                        CapacityStatusClass = statusClass,
                        IsMissingActual = !week.IsFuture && planned > 0 && !actual.HasValue,
                        CanEditForecast = canEditForecast,
                        CanEditActual = isPilotage || isCurrentResource,
                        CanSubmit = isPilotage || isCurrentResource,
                        CanReview = canValidateCharges
                    });
                }

                row.PlannedTotal = row.Cells.Sum(c => c.PlannedHours);
                row.ActualTotal = row.Cells.Where(c => c.ActualHours.HasValue).Sum(c => c.ActualHours ?? 0);
                row.VarianceTotal = row.ActualTotal - row.PlannedTotal;
                row.RemainingCapacity = row.CapacityTotal - row.ActualTotal;
                row.UtilizationRate = row.CapacityTotal > 0
                    ? Math.Round((double)(row.ActualTotal / row.CapacityTotal) * 100, 1)
                    : 0;

                var (rowStatus, rowStatusClass) = GetCapacityStatus(row.UtilizationRate);
                row.CapacityStatus = rowStatus;
                row.CapacityStatusClass = rowStatusClass;

                resourceRows.Add(row);
            }

            var weeklySummaries = new List<ProjetChargesWeeklySummaryViewModel>();
            foreach (var week in weekModels)
            {
                var cells = resourceRows.SelectMany(r => r.Cells).Where(c => c.WeekStart == week.StartDate).ToList();
                var planned = cells.Sum(c => c.PlannedHours);
                var actual = cells.Sum(c => c.ActualHours ?? 0);
                var capacity = ressources.Sum(r => r.CapaciteHebdomadaire);
                var missing = cells.Count(c => c.IsMissingActual);
                var pending = cells.Count(c => c.ValidationStatus == StatutValidationCharge.EnAttente);

                string status;
                string statusClass;
                if (week.IsFuture)
                {
                    status = "Prévision";
                    statusClass = "badge-modern-info";
                }
                else if (pending > 0)
                {
                    status = "À valider";
                    statusClass = "badge-modern-warning";
                }
                else if (missing > 0)
                {
                    status = "Saisie incomplète";
                    statusClass = "badge-modern-warning";
                }
                else if (actual > planned && planned > 0)
                {
                    status = "Dérive";
                    statusClass = "badge-modern-danger";
                }
                else
                {
                    status = "Saisie complète";
                    statusClass = "badge-modern-success";
                }

                weeklySummaries.Add(new ProjetChargesWeeklySummaryViewModel
                {
                    WeekStart = week.StartDate,
                    Label = $"{week.Label} · {week.Subtitle}",
                    PlannedTotal = planned,
                    ActualTotal = actual,
                    CapacityTotal = capacity,
                    MissingEntries = missing,
                    PendingValidations = pending,
                    Status = status,
                    StatusClass = statusClass
                });
            }

            var totalPlanned = resourceRows.Sum(r => r.PlannedTotal);
            var totalActual = resourceRows.Sum(r => r.ActualTotal);
            var totalCapacity = resourceRows.Sum(r => r.CapacityTotal);
            var totalVariance = totalActual - totalPlanned;

            var alerts = new List<ProjetChargeAlertViewModel>();
            var overloadedResources = resourceRows.Where(r => r.UtilizationRate > 100).ToList();
            if (overloadedResources.Any())
            {
                alerts.Add(new ProjetChargeAlertViewModel
                {
                    Level = "danger",
                    IconClass = "bi bi-exclamation-triangle-fill",
                    Message = $"{overloadedResources.Count} ressource(s) dépassent la capacité disponible sur la période affichée."
                });
            }

            var weeksPending = weeklySummaries.Count(w => w.PendingValidations > 0);
            if (weeksPending > 0)
            {
                alerts.Add(new ProjetChargeAlertViewModel
                {
                    Level = "warning",
                    IconClass = "bi bi-clock-history",
                    Message = $"{weeksPending} semaine(s) comportent des charges en attente de validation."
                });
            }

            var missingEntries = weeklySummaries.Sum(w => w.MissingEntries);
            if (missingEntries > 0)
            {
                alerts.Add(new ProjetChargeAlertViewModel
                {
                    Level = "warning",
                    IconClass = "bi bi-hourglass-split",
                    Message = $"{missingEntries} saisie(s) réelle(s) restent à compléter."
                });
            }

            if (totalActual > totalPlanned && totalPlanned > 0)
            {
                alerts.Add(new ProjetChargeAlertViewModel
                {
                    Level = "danger",
                    IconClass = "bi bi-graph-up-arrow",
                    Message = $"La charge réelle cumulée dépasse le prévisionnel de {(totalActual - totalPlanned):N1} h."
                });
            }

            if (projet.PourcentageAvancementAffiche < 50 && totalActual > 0 && totalPlanned > 0 && totalActual < (totalPlanned * 0.4m))
            {
                alerts.Add(new ProjetChargeAlertViewModel
                {
                    Level = "warning",
                    IconClass = "bi bi-slash-circle",
                    Message = "Le projet présente un avancement faible avec une consommation de charge anormale."
                });
            }

            if (projet.FicheProjet != null &&
                projet.FicheProjet.BudgetPrevisionnel.GetValueOrDefault() > 0 &&
                projet.FicheProjet.BudgetConsomme.GetValueOrDefault() > projet.FicheProjet.BudgetPrevisionnel.GetValueOrDefault())
            {
                alerts.Add(new ProjetChargeAlertViewModel
                {
                    Level = "warning",
                    IconClass = "bi bi-cash-stack",
                    Message = "Le budget consommé dépasse le budget prévisionnel du projet."
                });
            }

            var activities = activeCharges
                .Where(c => !string.IsNullOrWhiteSpace(c.Commentaire) || c.ChargeReelle.HasValue || !string.IsNullOrWhiteSpace(c.Activite))
                .OrderByDescending(c => c.DateSaisieChargeReelle ?? c.DateModification ?? c.DateCreation)
                .Take(12)
                .Select(c => new ProjetChargeActivityViewModel
                {
                    DateLabel = (c.DateSaisieChargeReelle ?? c.DateModification ?? c.DateCreation).ToString("dd/MM/yyyy"),
                    Resource = $"{c.Ressource?.Nom} {c.Ressource?.Prenoms}".Trim(),
                    Phase = projet.PhaseWorkflowLabel,
                    TypeActivite = string.IsNullOrWhiteSpace(c.TypeActivite) ? "Non précisé" : c.TypeActivite,
                    Activite = string.IsNullOrWhiteSpace(c.Activite) ? "Activité non renseignée." : c.Activite,
                    Hours = c.ChargeReelle ?? c.ChargePrevisionnelle,
                    Comment = string.IsNullOrWhiteSpace(c.Commentaire) ? "Aucun détail saisi." : c.Commentaire!
                })
                .ToList();

            return new ProjetChargesViewModel
            {
                ProjetId = projet.Id,
                CodeProjet = projet.CodeProjet,
                Titre = projet.Titre,
                Direction = projet.Direction?.Libelle ?? "Non définie",
                Sponsor = $"{projet.Sponsor?.Nom} {projet.Sponsor?.Prenoms}".Trim(),
                ChefProjet = projet.ChefProjet != null ? $"{projet.ChefProjet.Nom} {projet.ChefProjet.Prenoms}".Trim() : "Non affecté",
                Phase = projet.PhaseWorkflowLabel,
                Statut = projet.StatutWorkflowLabel,
                Avancement = projet.PourcentageAvancementAffiche,
                Etat = projet.EtatProjet.ToString(),
                ProchainJalon = projet.FicheProjet?.ProchainJalon ?? "À définir",
                BudgetPrevisionnel = projet.FicheProjet?.BudgetPrevisionnel ?? 0,
                BudgetConsomme = projet.FicheProjet?.BudgetConsomme ?? 0,
                BudgetEcart = (projet.FicheProjet?.BudgetConsomme ?? 0) - (projet.FicheProjet?.BudgetPrevisionnel ?? 0),
                ChargePrevisionnelleTotale = totalPlanned,
                ChargeReelleTotale = totalActual,
                ChargeRestanteEstimee = totalPlanned - totalActual,
                ChargeEcartTotale = totalVariance,
                CapaciteTotale = totalCapacity,
                TauxCapaciteUtilise = totalCapacity > 0 ? Math.Round((double)(totalActual / totalCapacity) * 100, 1) : 0,
                TauxConsommation = totalPlanned > 0 ? Math.Round((double)(totalActual / totalPlanned) * 100, 1) : 0,
                NombreRessources = resourceRows.Count,
                RessourcesSurchargees = overloadedResources.Count,
                ChargesEnAttenteValidation = activeCharges.Count(c => c.StatutValidation == StatutValidationCharge.EnAttente),
                CanEditForecast = canEditForecast,
                CanEditActual = canEditActual,
                CanValidateCharges = canValidateCharges,
                CanExport = canExport,
                Weeks = weekModels,
                Resources = resourceRows,
                WeeklySummaries = weeklySummaries,
                Alerts = alerts,
                Activities = activities
            };
        }

        private static (string Label, string CssClass) GetCapacityStatus(double utilizationRate)
        {
            if (utilizationRate > 100)
                return ("Surchargé", "badge-modern-danger");

            if (utilizationRate > 80)
                return ("Saturé", "badge-modern-warning");

            if (utilizationRate > 50)
                return ("Presque saturé", "badge-modern-info");

            return ("Disponible", "badge-modern-success");
        }

        private static string GetValidationChargeLabel(StatutValidationCharge statut)
        {
            return statut switch
            {
                StatutValidationCharge.Brouillon => "Brouillon",
                StatutValidationCharge.EnAttente => "En attente",
                StatutValidationCharge.Validee => "Validée",
                StatutValidationCharge.Commentee => "Commentée",
                StatutValidationCharge.Rejetee => "Rejetée",
                _ => "Brouillon"
            };
        }

        private static string GetValidationChargeClass(StatutValidationCharge statut)
        {
            return statut switch
            {
                StatutValidationCharge.Brouillon => "badge-modern-secondary",
                StatutValidationCharge.EnAttente => "badge-modern-warning",
                StatutValidationCharge.Validee => "badge-modern-success",
                StatutValidationCharge.Commentee => "badge-modern-info",
                StatutValidationCharge.Rejetee => "badge-modern-danger",
                _ => "badge-modern-secondary"
            };
        }

        private static string GetProfilRessourceLabel(ProfilRessource? profil)
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
                ProfilRessource.Autre => "Autre",
                _ => "Non défini"
            };
        }

        public async Task<ChargeProjet> SaisirAsync(
            Guid projetId, Guid ressourceId, DateTime semaineDebut,
            decimal? chargePrevisionnelle, decimal? chargeReelle,
            string? commentaire, string? typeActivite, string? activite,
            Guid userId, bool canEditForecast, bool canEditActual)
        {
            var lundiSemaine = NormalizeToMonday(semaineDebut);

            var charge = await _db.ChargesProjets
                .FirstOrDefaultAsync(c => c.ProjetId == projetId &&
                                          c.RessourceId == ressourceId &&
                                          c.SemaineDebut.Date == lundiSemaine.Date);

            if (charge == null)
            {
                charge = new ChargeProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projetId,
                    RessourceId = ressourceId,
                    SemaineDebut = lundiSemaine,
                    ChargePrevisionnelle = canEditForecast ? Math.Max(chargePrevisionnelle ?? 0, 0) : 0,
                    ChargeReelle = canEditActual ? chargeReelle : null,
                    DateSaisieChargeReelle = canEditActual && chargeReelle.HasValue ? DateTime.Now : null,
                    SaisieParId = canEditActual && chargeReelle.HasValue ? userId : null,
                    Commentaire = commentaire ?? string.Empty,
                    TypeActivite = typeActivite?.Trim() ?? string.Empty,
                    Activite = activite?.Trim() ?? string.Empty,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule
                };
                _db.ChargesProjets.Add(charge);
            }
            else
            {
                if (canEditForecast && chargePrevisionnelle.HasValue)
                    charge.ChargePrevisionnelle = Math.Max(chargePrevisionnelle.Value, 0);

                if (canEditActual)
                {
                    charge.ChargeReelle = chargeReelle;
                    charge.DateSaisieChargeReelle = chargeReelle.HasValue ? DateTime.Now : charge.DateSaisieChargeReelle;
                    charge.SaisieParId = chargeReelle.HasValue ? userId : charge.SaisieParId;
                    charge.Commentaire = commentaire ?? string.Empty;
                    charge.TypeActivite = typeActivite?.Trim() ?? string.Empty;
                    charge.Activite = activite?.Trim() ?? string.Empty;
                }

                charge.DateModification = DateTime.Now;
                charge.ModifiePar = _currentUserService.Matricule;
            }

            charge.StatutValidation = StatutValidationCharge.Brouillon;
            charge.DateSoumissionValidation = null;
            charge.DateValidation = null;
            charge.ValideeParId = null;
            charge.CommentaireValidation = string.Empty;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("UPDATE_CHARGE", "ChargeProjet", charge.Id,
                new { ProjetId = projetId, RessourceId = ressourceId, Semaine = lundiSemaine },
                new
                {
                    ChargePrevisionnelle = charge.ChargePrevisionnelle,
                    ChargeReelle = charge.ChargeReelle,
                    Commentaire = charge.Commentaire,
                    TypeActivite = charge.TypeActivite,
                    Activite = charge.Activite
                });

            return charge;
        }

        public async Task<ChargeProjet?> SoumettreAsync(Guid projetId, Guid ressourceId, DateTime semaineDebut)
        {
            var lundiSemaine = NormalizeToMonday(semaineDebut);
            var charge = await _db.ChargesProjets
                .FirstOrDefaultAsync(c => c.ProjetId == projetId &&
                                          c.RessourceId == ressourceId &&
                                          c.SemaineDebut.Date == lundiSemaine.Date);
            if (charge == null)
                return null;

            charge.StatutValidation = StatutValidationCharge.EnAttente;
            charge.DateSoumissionValidation = DateTime.Now;
            charge.DateModification = DateTime.Now;
            charge.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("SOUMISSION_CHARGE", "ChargeProjet", charge.Id);

            return charge;
        }

        public async Task<ChargeProjet?> MettreAJourValidationAsync(
            Guid projetId, Guid ressourceId, DateTime semaineDebut,
            StatutValidationCharge statut, string? commentaireValidation, Guid userId)
        {
            var lundiSemaine = NormalizeToMonday(semaineDebut);
            var charge = await _db.ChargesProjets
                .FirstOrDefaultAsync(c => c.ProjetId == projetId &&
                                          c.RessourceId == ressourceId &&
                                          c.SemaineDebut.Date == lundiSemaine.Date);
            if (charge == null)
                return null;

            charge.StatutValidation = statut;
            charge.DateValidation = DateTime.Now;
            charge.ValideeParId = userId;
            charge.CommentaireValidation = commentaireValidation?.Trim() ?? string.Empty;
            charge.DateModification = DateTime.Now;
            charge.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("VALIDATION_CHARGE", "ChargeProjet", charge.Id,
                new { Statut = statut.ToString(), Commentaire = charge.CommentaireValidation });

            return charge;
        }

        private static DateTime NormalizeToMonday(DateTime date)
            => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    }
}
