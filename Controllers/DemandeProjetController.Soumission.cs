using GestionProjects.Application.Common.Extensions;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class DemandeProjetController
    {
        // POST: Soumettre demande
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Soumettre(Guid id)
        {
            var demande = await _db.DemandesProjets
                .Include(d => d.Direction)
                .Include(d => d.Demandeur)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (demande.DemandeurId != userId && !await HasAdminScopeAsync())
                return Forbid();

            if (demande.StatutDemande != StatutDemande.Brouillon)
            {
                TempData["Error"] = "Cette demande ne peut plus être modifiée.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var demandesSimilaires = await DetecterDemandesSimilairesAsync(demande);
            if (demandesSimilaires.Any())
            {
                ViewBag.DemandesSimilaires = demandesSimilaires;
                ViewBag.DemandeCourante    = demande;
                return View("VerificationDoublons", demande);
            }

            await EnsurePortefeuilleActifAsync();

            var ancienStatut = demande.StatutDemande;
            demande.StatutDemande  = StatutDemande.EnAttenteValidationDirecteurMetier;
            demande.DateSoumission = DateTime.Now;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SOUMISSION_DEMANDE", "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut },
                new { StatutDemande = demande.StatutDemande });

            TempData["Success"] = "Demande soumise avec succès.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Confirmer soumission après vérification des doublons
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ConfirmerSoumission(Guid id, bool confirmerMalgreDoublons = false)
        {
            var demande = await _db.DemandesProjets.FindAsync(id);
            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (demande.DemandeurId != userId && !await HasAdminScopeAsync())
                return Forbid();

            if (demande.StatutDemande != StatutDemande.Brouillon)
            {
                TempData["Error"] = "Cette demande ne peut plus être modifiée.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!confirmerMalgreDoublons)
            {
                var demandesSimilaires = await DetecterDemandesSimilairesAsync(demande);
                if (demandesSimilaires.Any())
                {
                    ViewBag.DemandesSimilaires = demandesSimilaires;
                    ViewBag.DemandeCourante    = demande;
                    TempData["Warning"] = "Veuillez confirmer que vous souhaitez soumettre cette demande malgré l'existence de demandes similaires.";
                    return View("VerificationDoublons", demande);
                }
            }

            await EnsurePortefeuilleActifAsync();

            var ancienStatut = demande.StatutDemande;
            demande.StatutDemande  = StatutDemande.EnAttenteValidationDirecteurMetier;
            demande.DateSoumission = DateTime.Now;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("SOUMISSION_DEMANDE", "DemandeProjet", demande.Id,
                new { StatutDemande = ancienStatut, ConfirmeMalgreDoublons = confirmerMalgreDoublons },
                new { StatutDemande = demande.StatutDemande });

            TempData["Success"] = "Demande soumise avec succès.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Helper : s'assurer qu'un portefeuille actif existe
        private async Task EnsurePortefeuilleActifAsync()
        {
            var portefeuilleActif = await _db.PortefeuillesProjets.FirstOrDefaultAsync(p => p.EstActif && !p.EstSupprime);
            if (portefeuilleActif != null)
                return;

            portefeuilleActif = new PortefeuilleProjet
            {
                Id = Guid.NewGuid(),
                Nom = "Portefeuille de Projet DSI",
                ObjectifStrategiqueGlobal = "Assurer l'amélioration globale de l'efficacité opérationnelle et de la satisfaction des parties prenantes au Côte d'Ivoire Terminal.",
                AvantagesAttendus = @"• Gestion automatisée des notes de frais pour une amélioration de l'efficacité opérationnelle
• Mobilité des employés optimisée avec un système de réservation de bus efficace
• Opérations du centre médical optimisées
• Qualité du parcours client améliorée
• Suivi amélioré de la gestion des équipements
• Flux de contenu vers le scanner optimisé
• Suivi amélioré des demandes, conduisant à une meilleure visibilité des besoins et anticipation des ressources
• Efficacité de prise de décision améliorée grâce à la classification et priorisation des demandes basées sur des critères définis (budget, impact, urgence)
• Support utilisateur amélioré, réduisant le temps de résolution des incidents et augmentant la satisfaction utilisateur
• Gouvernance IT améliorée grâce à la mise en place de processus alignés avec les meilleures pratiques ITIL
• Gestion de la formation optimisée, incluant une planification centralisée, réduction des conflits d'horaires et suivi détaillé des sessions
• Conformité aux politiques de gouvernance IT, cybersécurité et groupe
• Sécurité et autonomie renforcées des échanges de données avec les partenaires externes via une solution SFTP",
                RisquesEtMitigations = @"Résistance des utilisateurs au changement: Atténué par la sensibilisation des utilisateurs, la formation et le support de déploiement
Retards possibles dans la livraison des composants logiciels: Atténué par la mise en place d'un processus de validation avec des critères clairs et un cadre commun
Risque d'interruption de service pendant la transition: Atténué par une planification détaillée avec une phase de test avant la migration finale et des plans de retour en arrière, ainsi que des campagnes de communication et de sensibilisation actives
Perte ou corruption de données pendant la migration: Atténué par un plan complet de sauvegarde et de restauration, et validation des données à chaque étape de migration
Risques de sécurité liés aux échanges de données sensibles: Atténué par l'implémentation de solutions sécurisées et conformes aux politiques de sécurité",
                EstActif = true,
                DateCreation = DateTime.Now,
                CreePar = _currentUserService.Matricule ?? "SYSTEM",
                EstSupprime = false
            };
            _db.PortefeuillesProjets.Add(portefeuilleActif);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("CREATION_PORTEFEUILLE", "PortefeuilleProjet", portefeuilleActif.Id,
                null, new { Nom = portefeuilleActif.Nom, EstActif = portefeuilleActif.EstActif });
        }
    }
}
