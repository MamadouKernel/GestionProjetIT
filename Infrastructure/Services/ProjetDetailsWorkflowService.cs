using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Application.ViewModels.Projet;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    public class ProjetDetailsWorkflowService : IProjetDetailsWorkflowService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;

        public ProjetDetailsWorkflowService(
            ApplicationDbContext db,
            IAuditService auditService,
            ICurrentUserService currentUserService)
        {
            _db = db;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        public async Task<WorkflowResult> UpdateChefProjetAsync(Guid projetId, Guid? chefProjetId)
        {
            var projet = await _db.Projets
                .Include(p => p.ChefProjet)
                .FirstOrDefaultAsync(p => p.Id == projetId);

            if (projet == null)
                return WorkflowResult.NotFound();

            if (projet.StatutProjet == StatutProjet.Cloture)
                return WorkflowResult.Error("Impossible de modifier le ResponsableSolutionsIT d'un projet clôturé.");

            var ancienChefProjetId = projet.ChefProjetId;
            var ancienChefProjetNom = projet.ChefProjet != null
                ? $"{projet.ChefProjet.Nom} {projet.ChefProjet.Prenoms}"
                : "Aucun";

            if (chefProjetId.HasValue)
            {
                var chefProjet = await _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles.Where(ur => !ur.EstSupprime))
                    .FirstOrDefaultAsync(u => u.Id == chefProjetId.Value && !u.EstSupprime);

                if (chefProjet == null)
                    return WorkflowResult.Error("Le ResponsableSolutionsIT sélectionné n'existe pas.");

                bool isValidChefProjet = chefProjet.UtilisateurRoles
                    .Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.ChefDeProjet);

                if (!isValidChefProjet)
                {
                    var delegationActive = await _db.DelegationsChefProjet
                        .AnyAsync(d => d.DelegueId == chefProjetId.Value &&
                                       d.ProjetId == projetId &&
                                       d.EstActive &&
                                       d.DateFin == null &&
                                       !d.EstSupprime);

                    if (!delegationActive)
                        return WorkflowResult.Error("Le ResponsableSolutionsIT sélectionné n'est pas valide (doit être ChefDeProjet ou avoir une délégation active pour ce projet).");
                }
            }

            // Clôturer l'entrée d'historique de l'ancien chef si changement
            if (ancienChefProjetId.HasValue && ancienChefProjetId != chefProjetId)
            {
                var historiqueAncienChef = await _db.HistoriqueChefProjets
                    .Where(h => h.ProjetId == projetId && h.ChefProjetId == ancienChefProjetId.Value && h.DateFin == null)
                    .FirstOrDefaultAsync();

                if (historiqueAncienChef != null)
                {
                    historiqueAncienChef.DateFin = DateTime.Now;
                    historiqueAncienChef.DateModification = DateTime.Now;
                    historiqueAncienChef.ModifiePar = _currentUserService.Matricule;
                }
            }

            projet.ChefProjetId = chefProjetId;
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule ?? "SYSTEM";

            await _db.SaveChangesAsync();

            if (chefProjetId.HasValue && ancienChefProjetId != chefProjetId)
            {
                _db.HistoriqueChefProjets.Add(new HistoriqueChefProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    ChefProjetId = chefProjetId.Value,
                    DateDebut = DateTime.Now,
                    DateFin = null,
                    Commentaire = "Assignation comme chef de projet",
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM"
                });
                await _db.SaveChangesAsync();
            }

            var nouveauChefProjetNom = chefProjetId.HasValue
                ? await _db.Utilisateurs
                    .Where(u => u.Id == chefProjetId.Value)
                    .Select(u => $"{u.Nom} {u.Prenoms}")
                    .FirstOrDefaultAsync()
                : "Aucun";

            await _auditService.LogActionAsync("UPDATE_CHEFPROJET", "Projet", projet.Id,
                new { AncienChefProjetId = ancienChefProjetId, AncienChefProjet = ancienChefProjetNom },
                new { NouveauChefProjetId = chefProjetId, NouveauChefProjet = nouveauChefProjetNom });

            return WorkflowResult.Success(chefProjetId.HasValue
                ? $"ResponsableSolutionsIT mis à jour : {nouveauChefProjetNom}"
                : "ResponsableSolutionsIT retiré du projet.");
        }

        public async Task<WorkflowResult> DemarrerProjetAsync(Guid projetId, Guid userId)
        {
            var projet = await _db.Projets.FirstOrDefaultAsync(p => p.Id == projetId);
            if (projet == null)
                return WorkflowResult.NotFound();

            if (projet.StatutProjet == StatutProjet.Cloture || projet.StatutProjet == StatutProjet.Annule)
                return WorkflowResult.Error("Impossible de demarrer un projet cloture ou annule.");

            if (projet.StatutProjet != StatutProjet.NonDemarre)
                return WorkflowResult.Error("Ce projet est deja demarre.");

            if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
                return WorkflowResult.Error("Un projet ne peut demarrer que depuis la phase Analyse & Clarification.");

            if (!projet.ChefProjetId.HasValue)
                return WorkflowResult.Error("Assignez d'abord un ResponsableSolutionsIT avant de demarrer le projet.");

            var ancienStatut = projet.StatutProjet;
            var ancienPourcentage = projet.PourcentageAvancement;

            projet.StatutProjet = StatutProjet.EnCours;
            projet.PourcentageAvancement = 0;
            projet.DateDebut ??= DateTime.Now;
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule ?? "SYSTEM";

            _db.HistoriquePhasesProjets.Add(new HistoriquePhaseProjet
            {
                Id = Guid.NewGuid(),
                ProjetId = projet.Id,
                Phase = projet.PhaseActuelle,
                StatutProjet = projet.StatutProjet,
                DateDebut = projet.DateDebut.Value,
                ModifieParId = userId,
                Commentaire = "Demarrage operationnel du projet - prise en charge reelle",
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule ?? "SYSTEM"
            });

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("DEMARRAGE_PROJET", "Projet", projet.Id,
                new { StatutProjet = ancienStatut, PourcentageAvancement = ancienPourcentage },
                new { projet.StatutProjet, projet.PourcentageAvancement, projet.DateDebut, projet.ChefProjetId });

            return WorkflowResult.Success("Projet demarre. L'avancement reste a 0% jusqu'a la production des premiers livrables.");
        }

        public async Task<ProjetDetailsViewModel> BuildDetailsViewModelAsync(
            Projet projet,
            Guid userId,
            string? tab,
            bool isReadOnly,
            bool canReassignChefProjet,
            bool isAssignedChefProjet,
            bool canOpenChargesTab)
        {
            var id = projet.Id;

            List<CasTestProjet> casTests = new();
            List<CampagneTestProjet> campagnes = new();
            CollaborationProjet? collaboration = null;
            List<DossierSignatureProjet> dossiersSignature = new();
            IEnumerable<AuditLog> auditLogs = Enumerable.Empty<AuditLog>();
            List<Utilisateur> chefsProjet = new();

            if (tab == "uat")
            {
                casTests = await _db.CasTestsProjets
                    .Include(c => c.Executions.OrderByDescending(e => e.DateExecution))
                    .Include(c => c.CampagneTestProjet)
                    .Where(c => c.ProjetId == id && !c.EstSupprime)
                    .OrderBy(c => c.Reference)
                    .ToListAsync();

                campagnes = await _db.CampagnesTestsProjets
                    .Where(c => c.ProjetId == id && !c.EstSupprime)
                    .OrderByDescending(c => c.DateLancement)
                    .ToListAsync();
            }

            if (tab == "collaboration" || tab == "execution")
            {
                collaboration = await _db.CollaborationsProjets
                    .Include(c => c.Taches.OrderBy(t => t.Phase))
                    .FirstOrDefaultAsync(c => c.ProjetId == id && !c.EstSupprime);
            }

            if (tab == "planification")
            {
                dossiersSignature = await _db.DossiersSignatureProjets
                    .Include(d => d.Signataires.OrderBy(s => s.OrdreSignature))
                        .ThenInclude(s => s.Utilisateur)
                    .Where(d => d.ProjetId == id && !d.EstSupprime)
                    .OrderByDescending(d => d.DateCreation)
                    .ToListAsync();
            }

            if (tab == "historique")
            {
                auditLogs = await _db.AuditLogs
                    .Include(a => a.Utilisateur)
                    .Where(a => a.Entite == "Projet" && a.EntiteId == id.ToString())
                    .OrderByDescending(a => a.DateAction)
                    .ToListAsync();
            }

            if (canReassignChefProjet)
            {
                var chefsProjetBase = await _db.Utilisateurs
                    .Include(u => u.UtilisateurRoles)
                    .Where(u => !u.EstSupprime && u.UtilisateurRoles.Any(ur => !ur.EstSupprime && ur.Role == RoleUtilisateur.ChefDeProjet))
                    .OrderBy(u => u.Nom)
                    .ThenBy(u => u.Prenoms)
                    .ToListAsync();

                var delegationsActives = await _db.DelegationsChefProjet
                    .Include(d => d.Delegue)
                    .Where(d => !d.EstSupprime &&
                                d.EstActive &&
                                d.ProjetId == id &&
                                d.DateDebut <= DateTime.Now &&
                                d.DateFin == null)
                    .Select(d => d.Delegue!)
                    .Where(u => !u.EstSupprime)
                    .ToListAsync();

                chefsProjet = chefsProjetBase
                    .Union(delegationsActives)
                    .GroupBy(u => u.Id)
                    .Select(g => g.First())
                    .OrderBy(u => u.Nom)
                    .ThenBy(u => u.Prenoms)
                    .ToList();
            }

            if (isAssignedChefProjet)
            {
                var priseEnChargeExiste = await _db.AuditLogs
                    .AnyAsync(a => a.Entite == "Projet" &&
                                  a.EntiteId == id.ToString() &&
                                  a.TypeAction == "PRISE_EN_CHARGE_PROJET" &&
                                  a.UtilisateurId == userId);

                if (!priseEnChargeExiste)
                {
                    await _auditService.LogActionAsync("PRISE_EN_CHARGE_PROJET", "Projet", projet.Id,
                        null,
                        new { ChefProjetId = userId, CodeProjet = projet.CodeProjet });
                }
            }

            var isDemandeurProject = projet.DemandeProjet?.DemandeurId == userId;
            var canAccessCharges = canOpenChargesTab;

            return new ProjetDetailsViewModel
            {
                Projet             = projet,
                ActiveTab          = tab ?? "synthese",
                IsReadOnly         = isReadOnly,
                IsDemandeurProject = isDemandeurProject,
                CanAccessCharges   = canAccessCharges,
                CasTests           = casTests,
                Campagnes          = campagnes,
                Collaboration      = collaboration,
                DossiersSignature  = dossiersSignature,
                AuditLogs          = auditLogs,
                ChefsProjet        = chefsProjet,
            };
        }
    }
}
