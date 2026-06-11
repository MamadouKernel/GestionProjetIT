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
        // POST: Ajouter des documents complémentaires
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("UploadPolicy")]
        public async Task<IActionResult> AjouterDocumentsComplementaires(Guid id, List<IFormFile>? documents)
        {
            var demande = await _db.DemandesProjets
                .Include(d => d.Annexes)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (demande == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var canManageDemandes = await CanManageDemandesBackofficeAsync();

            if (demande.DemandeurId != userId && !canManageDemandes)
                return Forbid();

            if (demande.StatutDemande != StatutDemande.CorrectionDemandeeParDirecteurMetier &&
                demande.StatutDemande != StatutDemande.RetourneeAuDemandeurParDSI &&
                demande.StatutDemande != StatutDemande.EnAttenteValidationDirecteurMetier &&
                demande.StatutDemande != StatutDemande.EnAttenteValidationDSI &&
                !canManageDemandes)
            {
                TempData["Error"] = "Vous ne pouvez ajouter des documents que lorsque la demande est en attente de compléments ou en cours de traitement.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (documents != null && documents.Any())
            {
                var documentsAjoutes = 0;
                foreach (var document in documents)
                {
                    if (document.Length > 0)
                    {
                        var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".png" };
                        var maxSize = 10 * 1024 * 1024;
                        var path = await _fileStorage.SaveFileAsync(document, "demandes", demande.Id.ToString(), allowedExtensions, maxSize);

                        _db.DocumentsJointsDemandes.Add(new DocumentJointDemande
                        {
                            Id              = Guid.NewGuid(),
                            DemandeProjetId = demande.Id,
                            NomFichier      = document.FileName,
                            CheminRelatif   = path,
                            DateDepot       = DateTime.Now,
                            DeposeParId     = userId,
                            DateCreation    = DateTime.Now,
                            CreePar         = _currentUserService.Matricule
                        });
                        documentsAjoutes++;
                    }
                }

                if (documentsAjoutes > 0)
                {
                    if (demande.StatutDemande == StatutDemande.CorrectionDemandeeParDirecteurMetier)
                    {
                        demande.StatutDemande = StatutDemande.EnAttenteValidationDirecteurMetier;
                        await _auditService.LogActionAsync("COMPLEMENT_INFORMATION_AJOUTE", "DemandeProjet", demande.Id,
                            new { AncienStatut = StatutDemande.CorrectionDemandeeParDirecteurMetier, NouveauStatut = demande.StatutDemande, NombreDocuments = documentsAjoutes });
                    }
                    else if (demande.StatutDemande == StatutDemande.RetourneeAuDemandeurParDSI)
                    {
                        demande.StatutDemande = StatutDemande.EnAttenteValidationDSI;
                        await _auditService.LogActionAsync("COMPLEMENT_INFORMATION_AJOUTE", "DemandeProjet", demande.Id,
                            new { AncienStatut = StatutDemande.RetourneeAuDemandeurParDSI, NouveauStatut = demande.StatutDemande, NombreDocuments = documentsAjoutes });
                    }
                    else
                    {
                        await _auditService.LogActionAsync("AJOUT_DOCUMENTS_COMPLEMENTAIRES", "DemandeProjet", demande.Id,
                            new { NombreDocuments = documentsAjoutes });
                    }

                    await _db.SaveChangesAsync();
                    TempData["Success"] = $"{documentsAjoutes} document(s) complémentaire(s) ajouté(s) avec succès. La demande a été remise en validation.";
                }
                else
                {
                    TempData["Error"] = "Aucun document valide n'a été ajouté.";
                }
            }
            else
            {
                TempData["Error"] = "Veuillez sélectionner au moins un document.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Dupliquer/Relancer une demande refusée
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DupliquerDemande(Guid id)
        {
            var demandeOriginale = await _db.DemandesProjets.Include(d => d.Annexes).FirstOrDefaultAsync(d => d.Id == id);
            if (demandeOriginale == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            var canManageDemandes = await CanManageDemandesBackofficeAsync();

            if (demandeOriginale.DemandeurId != userId && !canManageDemandes)
                return Forbid();

            if (demandeOriginale.StatutDemande != StatutDemande.RejeteeParDirecteurMetier &&
                demandeOriginale.StatutDemande != StatutDemande.RejeteeParDSI)
            {
                TempData["Error"] = "Seules les demandes refusées peuvent être dupliquées.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var nouvelleDemande = new DemandeProjet
            {
                Id                         = Guid.NewGuid(),
                DemandeurId                = demandeOriginale.DemandeurId,
                DirectionId                = demandeOriginale.DirectionId,
                DirecteurMetierId          = demandeOriginale.DirecteurMetierId,
                Titre                      = demandeOriginale.Titre,
                Description                = demandeOriginale.Description,
                Contexte                   = demandeOriginale.Contexte,
                Objectifs                  = demandeOriginale.Objectifs,
                AvantagesAttendus          = demandeOriginale.AvantagesAttendus,
                Perimetre                  = demandeOriginale.Perimetre,
                Urgence                    = demandeOriginale.Urgence,
                Criticite                  = demandeOriginale.Criticite,
                DateMiseEnOeuvreSouhaitee  = demandeOriginale.DateMiseEnOeuvreSouhaitee,
                StatutDemande              = StatutDemande.Brouillon,
                DateSoumission             = DateTime.Now,
                DateCreation               = DateTime.Now,
                CreePar                    = _currentUserService.Matricule,
                CahierChargesPath          = string.Empty,
                CommentaireDirecteurMetier = string.Empty,
                CommentaireDSI             = string.Empty
            };

            _db.DemandesProjets.Add(nouvelleDemande);
            await _db.SaveChangesAsync();

            if (demandeOriginale.Annexes != null && demandeOriginale.Annexes.Any())
            {
                foreach (var annexe in demandeOriginale.Annexes)
                {
                    _db.DocumentsJointsDemandes.Add(new DocumentJointDemande
                    {
                        Id              = Guid.NewGuid(),
                        DemandeProjetId = nouvelleDemande.Id,
                        NomFichier      = annexe.NomFichier,
                        CheminRelatif   = annexe.CheminRelatif,
                        DateDepot       = DateTime.Now,
                        DeposeParId     = userId,
                        DateCreation    = DateTime.Now,
                        CreePar         = _currentUserService.Matricule
                    });
                }
                await _db.SaveChangesAsync();
            }

            await _auditService.LogActionAsync("RELANCE_DEMANDE_PROJET", "DemandeProjet", nouvelleDemande.Id,
                new { DemandeOriginaleId = demandeOriginale.Id, StatutOriginal = demandeOriginale.StatutDemande });

            TempData["Success"] = "Demande dupliquée avec succès. Vous pouvez maintenant la modifier et la soumettre.";
            return RedirectToAction(nameof(Details), new { id = nouvelleDemande.Id });
        }
    }
}
