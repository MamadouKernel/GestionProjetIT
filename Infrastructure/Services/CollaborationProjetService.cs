using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    public class CollaborationProjetService : ICollaborationProjetService
    {
        private readonly ApplicationDbContext _context;

        public CollaborationProjetService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CollaborationProjet> ConfigurerAsync(Guid projetId, CollaborationProjetConfigurationRequest request, string? currentUserMatricule)
        {
            var projet = await _context.Projets
                .Include(p => p.ChefProjet)
                .FirstOrDefaultAsync(p => p.Id == projetId);

            if (projet == null)
                throw new InvalidOperationException("Projet introuvable.");

            var collaboration = await _context.Set<CollaborationProjet>()
                .Include(c => c.Taches)
                .FirstOrDefaultAsync(c => c.ProjetId == projetId);

            if (collaboration == null)
            {
                collaboration = new CollaborationProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projetId,
                    DateCreation = DateTime.Now,
                    CreePar = currentUserMatricule ?? "SYSTEM"
                };
                _context.Add(collaboration);
            }

            collaboration.Mode = request.Mode;
            collaboration.Statut = StatutCollaborationProjet.Configuree;
            collaboration.NomEquipeTeams = ValeurOuDefaut(request.NomEquipeTeams, $"Equipe {projet.CodeProjet}");
            collaboration.TeamId = Nettoyer(request.TeamId);
            collaboration.TeamUrl = Nettoyer(request.TeamUrl);
            collaboration.NomCanalTeams = ValeurOuDefaut(request.NomCanalTeams, projet.CodeProjet);
            collaboration.ChannelId = Nettoyer(request.ChannelId);
            collaboration.ChannelUrl = Nettoyer(request.ChannelUrl);
            collaboration.NomPlanPlanner = ValeurOuDefaut(request.NomPlanPlanner, $"Plan {projet.CodeProjet}");
            collaboration.PlanId = Nettoyer(request.PlanId);
            collaboration.PlanUrl = Nettoyer(request.PlanUrl);
            collaboration.NomBucketPlanner = Nettoyer(request.NomBucketPlanner) ?? "Backlog Projet";
            collaboration.BucketId = Nettoyer(request.BucketId);
            collaboration.DateProvisioning ??= DateTime.Now;
            collaboration.MessageStatut = request.Mode == ModeCollaborationProjet.Microsoft365
                ? "Espace Teams / Planner configure. Synchronisez l'equipe pour creer ou mettre a jour les taches de phase."
                : "Espace de collaboration renseigne en mode manuel.";
            collaboration.DateModification = DateTime.Now;
            collaboration.ModifiePar = currentUserMatricule;

            AssurerTachesParDefaut(projet, collaboration, currentUserMatricule);
            await _context.SaveChangesAsync();
            return collaboration;
        }

        public async Task<CollaborationProjetSyncResult> SynchroniserAsync(Guid projetId, string? currentUserMatricule)
        {
            var projet = await _context.Projets
                .Include(p => p.Membres)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .FirstOrDefaultAsync(p => p.Id == projetId);

            if (projet == null)
                throw new InvalidOperationException("Projet introuvable.");

            var collaboration = await _context.Set<CollaborationProjet>()
                .Include(c => c.Taches)
                .FirstOrDefaultAsync(c => c.ProjetId == projetId);

            if (collaboration == null)
            {
                return new CollaborationProjetSyncResult
                {
                    Success = false,
                    Message = "Configurez d'abord l'espace Teams / Planner du projet."
                };
            }

            AssurerTachesParDefaut(projet, collaboration, currentUserMatricule);

            var nombreMembres = CalculerNombreMembres(projet);
            collaboration.NombreMembresSynchronises = nombreMembres;
            collaboration.DerniereSynchronisationEquipe = DateTime.Now;
            collaboration.Statut = StatutCollaborationProjet.Synchronisee;
            collaboration.MessageStatut = $"Synchronisation effectuee. {nombreMembres} membre(s) et {collaboration.Taches.Count} tache(s) de phase suivis.";
            collaboration.DateModification = DateTime.Now;
            collaboration.ModifiePar = currentUserMatricule;

            foreach (var tache in collaboration.Taches)
            {
                tache.AssigneeId ??= projet.ChefProjetId;
                tache.ExternalTaskId ??= $"TASK-{projet.CodeProjet}-{(int)tache.Phase}";
                tache.ExternalBucketId ??= collaboration.BucketId;
                tache.EstSynchronisee = true;
                tache.Statut = DeterminerStatutTache(tache.Phase, projet.PhaseActuelle, projet.StatutProjet);
                tache.DateModification = DateTime.Now;
                tache.ModifiePar = currentUserMatricule;
            }

            await _context.SaveChangesAsync();

            return new CollaborationProjetSyncResult
            {
                Success = true,
                Message = collaboration.MessageStatut,
                NombreMembresSynchronises = collaboration.NombreMembresSynchronises,
                NombreTachesSynchronisees = collaboration.Taches.Count,
                Collaboration = collaboration
            };
        }

        private void AssurerTachesParDefaut(Projet projet, CollaborationProjet collaboration, string? currentUserMatricule)
        {
            var phases = new[]
            {
                PhaseProjet.AnalyseClarification,
                PhaseProjet.PlanificationValidation,
                PhaseProjet.ExecutionSuivi,
                PhaseProjet.UatMep,
                PhaseProjet.ClotureLeconsApprises
            };

            foreach (var phase in phases)
            {
                if (collaboration.Taches.Any(t => t.Phase == phase))
                    continue;

                var tache = new TacheCollaborationProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    CollaborationProjetId = collaboration.Id,
                    Phase = phase,
                    Titre = $"{projet.CodeProjet} - {LibellePhase(phase)}",
                    Statut = DeterminerStatutTache(phase, projet.PhaseActuelle, projet.StatutProjet),
                    DateEcheance = EstimerDateEcheance(projet, phase),
                    AssigneeId = projet.ChefProjetId,
                    EstSynchronisee = false,
                    DateCreation = DateTime.Now,
                    CreePar = currentUserMatricule ?? "SYSTEM"
                };
                collaboration.Taches.Add(tache);
            }
        }

        private static int CalculerNombreMembres(Projet projet)
        {
            var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(projet.Sponsor?.Email))
                emails.Add(projet.Sponsor.Email);

            if (!string.IsNullOrWhiteSpace(projet.ChefProjet?.Email))
                emails.Add(projet.ChefProjet.Email);

            foreach (var membre in projet.Membres.Where(m => m.EstActif && !string.IsNullOrWhiteSpace(m.Email)))
                emails.Add(membre.Email);

            return emails.Count;
        }

        private static StatutTacheCollaborationProjet DeterminerStatutTache(PhaseProjet phaseTache, PhaseProjet phaseActuelle, StatutProjet statutProjet)
        {
            if (statutProjet == StatutProjet.Cloture)
                return StatutTacheCollaborationProjet.Terminee;

            var ordre = new[]
            {
                PhaseProjet.AnalyseClarification,
                PhaseProjet.PlanificationValidation,
                PhaseProjet.ExecutionSuivi,
                PhaseProjet.UatMep,
                PhaseProjet.ClotureLeconsApprises
            };

            var indexTache = Array.IndexOf(ordre, phaseTache);
            var indexActuel = Array.IndexOf(ordre, phaseActuelle);

            if (indexTache < indexActuel)
                return StatutTacheCollaborationProjet.Terminee;

            if (indexTache == indexActuel)
                return StatutTacheCollaborationProjet.EnCours;

            return StatutTacheCollaborationProjet.APlanifier;
        }

        private static DateTime? EstimerDateEcheance(Projet projet, PhaseProjet phase)
        {
            if (projet.DateFinPrevue.HasValue)
                return projet.DateFinPrevue;

            if (projet.DateDebut.HasValue)
            {
                var offset = phase switch
                {
                    PhaseProjet.AnalyseClarification => 14,
                    PhaseProjet.PlanificationValidation => 30,
                    PhaseProjet.ExecutionSuivi => 90,
                    PhaseProjet.UatMep => 120,
                    PhaseProjet.ClotureLeconsApprises => 140,
                    _ => 30
                };

                return projet.DateDebut.Value.AddDays(offset);
            }

            return null;
        }

        private static string LibellePhase(PhaseProjet phase)
        {
            return phase switch
            {
                PhaseProjet.AnalyseClarification => "Analyse et Clarification",
                PhaseProjet.PlanificationValidation => "Planification",
                PhaseProjet.ExecutionSuivi => "Execution et Suivi",
                PhaseProjet.UatMep => "UAT et Mise en Production",
                PhaseProjet.ClotureLeconsApprises => "Cloture et Lecons Apprises",
                _ => phase.ToString()
            };
        }

        private static string ValeurOuDefaut(string? valeur, string valeurDefaut)
        {
            return string.IsNullOrWhiteSpace(valeur) ? valeurDefaut : valeur.Trim();
        }

        private static string? Nettoyer(string? valeur)
        {
            return string.IsNullOrWhiteSpace(valeur) ? null : valeur.Trim();
        }
    }
}
