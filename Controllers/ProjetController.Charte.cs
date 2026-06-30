using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.Common.Results;
using GestionProjects.Application.ViewModels.Projet;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class ProjetController
    {
        // GET: Afficher/Éditer la charte de projet
        [Authorize]
        public async Task<IActionResult> CharteProjet(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanViewProject)
            {
                return Forbid();
            }

            var vm = await _charteWorkflow.ObtenirPourAffichageAsync(projet);

            return View(vm);
        }

        // POST: Sauvegarder la charte de projet
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SauvegarderCharteProjet(Guid id, CharteProjet charte,
            List<JalonCharte>? Jalons = null, List<PartiePrenanteCharte>? PartiesPrenantes = null)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            NormalizeCharteProjetForPersistence(charte);

            if (!await CanManageAnalyseAsync(projet))
            {
                return Forbid();
            }

            var result = await _charteWorkflow.SauvegarderAsync(id, charte, Jalons, PartiesPrenantes, User.GetUserIdOrThrow());
            if (result.IsNotFound)
                return NotFound();

            if (result.ErrorMessage is not null)
                TempData["Error"] = result.ErrorMessage;
            else
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(CharteProjet), new { id });
        }

        // POST: Générer PDF Charte
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> GenererChartePdf(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Direction)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.DemandeProjet)
                .Include(p => p.Membres)
                .Include(p => p.Risques)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            if (!await CanManageAnalyseAsync(projet))
                return Forbid();

            var userId = User.GetUserIdOrThrow();

            try
            {
                var pdfBytes = await _pdfService.GenerateCharteProjetPdfAsync(projet);

                // Sauvegarder le PDF comme livrable
                var fileName = $"CharteProjet_{projet.CodeProjet}_{DateTime.Now:yyyyMMdd}.pdf";
                var filePath = Path.Combine("projets", projet.CodeProjet, "analyse", fileName);
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", filePath);

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await System.IO.File.WriteAllBytesAsync(fullPath, pdfBytes);

                var livrable = new LivrableProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    Phase = PhaseProjet.AnalyseClarification,
                    TypeLivrable = TypeLivrable.CharteProjet,
                    NomDocument = fileName,
                    CheminRelatif = Path.Combine("uploads", filePath).Replace('\\', '/'),
                    DateDepot = DateTime.Now,
                    DeposeParId = userId,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule
                };

                _db.LivrablesProjets.Add(livrable);
                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync("GENERATION_CHARTE_PDF", "Projet", projet.Id);

                Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la génération du PDF: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id, tab = "analyse" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> InitialiserDossierSignatureCharte(Guid id, FournisseurSignatureElectronique fournisseur)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            if (!await CanViewProjectAsync(projet))
            {
                return Forbid();
            }

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanManageDossierSignature)
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            {
                TempData["Error"] = "Le dossier de signature de la charte ne peut être initialisé qu'en phase Analyse.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            try
            {
                var result = await _electronicSignature.InitialiserCharteAsync(id, fournisseur, _currentUserService.Matricule);
                TempData["Success"] = result != null
                    ? "Dossier de signature de la charte initialisé."
                    : "Impossible d'initialiser le dossier de signature.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de l'initialisation du dossier : {ex.Message}";
            }

            return RedirectToAction(nameof(CharteProjet), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> EnvoyerDossierSignatureCharte(Guid id, Guid dossierSignatureId)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanManageDossierSignature)
                return Forbid();

            var dossier = await _db.DossiersSignatureProjets
                .FirstOrDefaultAsync(d => d.Id == dossierSignatureId &&
                                          d.ProjetId == id &&
                                          d.TypeDocument == TypeDocumentSignatureProjet.CharteProjet &&
                                          !d.EstSupprime);
            if (dossier == null)
            {
                TempData["Error"] = "Le dossier de signature de la charte est introuvable.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            try
            {
                var result = await _electronicSignature.EnvoyerDossierAsync(dossierSignatureId, _currentUserService.Matricule);
                TempData[result.Success ? "Success" : "Error"] = result.Message;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de l'envoi du dossier : {ex.Message}";
            }

            return RedirectToAction(nameof(CharteProjet), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> MettreAJourSignataireDossierSignatureCharte(Guid id, Guid dossierSignatureId, Guid signataireId, bool approuver)
        {
            var projet = await _db.Projets
                .Include(p => p.CharteProjet)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var dossier = await _db.DossiersSignatureProjets
                .Include(d => d.Signataires)
                .FirstOrDefaultAsync(d => d.Id == dossierSignatureId &&
                                          d.ProjetId == id &&
                                          d.TypeDocument == TypeDocumentSignatureProjet.CharteProjet &&
                                          !d.EstSupprime);
            if (dossier == null)
            {
                TempData["Error"] = "Le dossier de signature de la charte est introuvable.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            var signataire = dossier.Signataires.FirstOrDefault(s => s.Id == signataireId);
            if (signataire == null)
            {
                TempData["Error"] = "Le signataire sélectionné est introuvable.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            var userId = User.GetUserIdOrThrow();
            var ui = await BuildProjectUiAsync(projet);
            var canManageForProject = ui.CanManageDossierSignature;
            var isCurrentSignatory = signataire.UtilisateurId == userId;
            var isSponsorDecisionByDm = await CanValidateCharteAsDirecteurMetierAsync(projet, userId) &&
                                        projet.SponsorId == userId &&
                                        signataire.Role == RoleSignataireProjet.Sponsor;

            if (!canManageForProject && !isCurrentSignatory && !isSponsorDecisionByDm)
                return Forbid();

            try
            {
                var result = await _electronicSignature.EnregistrerDecisionSignataireAsync(
                    dossierSignatureId, signataireId, approuver, _currentUserService.Matricule);

                if (result.Success && projet.CharteProjet != null)
                {
                    if (signataire.Role == RoleSignataireProjet.Sponsor)
                    {
                        projet.CharteProjet.SignatureSponsor = approuver;
                        projet.CharteProjet.DateSignatureSponsor = approuver ? (signataire.DateSignature ?? DateTime.Now) : null;
                        projet.CharteProjet.SignatureSponsorId = approuver ? (signataire.UtilisateurId ?? projet.SponsorId) : null;
                        if (!approuver)
                        {
                            projet.CharteProjet.SignatureImageSponsor = null;
                            projet.CharteProjet.DateSignatureImageSponsor = null;
                        }
                    }

                    if (signataire.Role == RoleSignataireProjet.ChefDeProjet)
                    {
                        projet.CharteProjet.SignatureChefProjet = approuver;
                        projet.CharteProjet.DateSignatureChefProjet = approuver ? (signataire.DateSignature ?? DateTime.Now) : null;
                        projet.CharteProjet.SignatureChefProjetId = approuver ? (signataire.UtilisateurId ?? projet.ChefProjetId) : null;
                        if (!approuver)
                        {
                            projet.CharteProjet.SignatureImageCP = null;
                            projet.CharteProjet.DateSignatureImageCP = null;
                        }
                    }

                    ResetCharteValidationState(projet);
                    projet.DateModification = DateTime.Now;
                    projet.ModifiePar = _currentUserService.Matricule;
                    projet.CharteProjet.DateModification = DateTime.Now;
                    projet.CharteProjet.ModifiePar = _currentUserService.Matricule;
                    await _db.SaveChangesAsync();
                }

                TempData[result.Success ? "Success" : "Error"] = result.Message;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erreur lors de la mise à jour de la signature : {ex.Message}";
            }

            return RedirectToAction(nameof(CharteProjet), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("UploadPolicy")]
        public async Task<IActionResult> UploadCharteSignee(
            Guid id,
            IFormFile fichierSigne,
            string? version,
            string? commentaire,
            bool signatureSponsor = false,
            bool signatureChefProjet = false,
            Guid? dossierSignatureId = null)
        {
            var projet = await _db.Projets
                .Include(p => p.CharteProjet)
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanManageDossierSignature)
                return Forbid();

            var userId = User.GetUserIdOrThrow();

            if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            {
                TempData["Error"] = "Le dépôt de la charte signée est réservé à la phase Analyse.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            if (fichierSigne == null || fichierSigne.Length == 0)
            {
                TempData["Error"] = "Veuillez sélectionner le document signé à déposer.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
            if (!_fileStorage.IsValidFileExtension(fichierSigne.FileName, allowedExtensions))
            {
                TempData["Error"] = "Extension de fichier non autorisée pour la charte signée.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            DossierSignatureProjet? dossierSignature = null;
            if (dossierSignatureId.HasValue)
            {
                dossierSignature = await _db.DossiersSignatureProjets
                    .Include(d => d.Signataires)
                    .FirstOrDefaultAsync(d => d.Id == dossierSignatureId.Value &&
                                              d.ProjetId == id &&
                                              d.TypeDocument == TypeDocumentSignatureProjet.CharteProjet &&
                                              !d.EstSupprime);
                if (dossierSignature == null)
                {
                    TempData["Error"] = "Le dossier de signature sélectionné est introuvable.";
                    return RedirectToAction(nameof(CharteProjet), new { id });
                }

                if (!AreRequiredCharteSignaturesCompleted(dossierSignature))
                {
                    TempData["Error"] = "Le document signé ne peut être versé qu'après la signature du Sponsor et du Chef de Projet.";
                    return RedirectToAction(nameof(CharteProjet), new { id });
                }
            }

            if (projet.CharteProjet == null)
            {
                TempData["Error"] = "La charte doit être initialisée avant de déposer sa version signée.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            var maxSize = 10 * 1024 * 1024;
            var path = await _fileStorage.SaveFileAsync(
                fichierSigne,
                $"projets/{projet.CodeProjet}/analyse/charte-signee",
                null,
                allowedExtensions,
                maxSize);

            var livrable = projet.Livrables
                .Where(l => !l.EstSupprime && l.TypeLivrable == TypeLivrable.CharteProjetSignee)
                .OrderByDescending(l => l.DateDepot)
                .FirstOrDefault();

            if (livrable == null)
            {
                livrable = new LivrableProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = id,
                    Phase = PhaseProjet.AnalyseClarification,
                    TypeLivrable = TypeLivrable.CharteProjetSignee,
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule
                };
                _db.LivrablesProjets.Add(livrable);
            }
            else
            {
                livrable.DateModification = DateTime.Now;
                livrable.ModifiePar = _currentUserService.Matricule;
            }

            livrable.NomDocument = fichierSigne.FileName;
            livrable.CheminRelatif = path;
            livrable.DateDepot = DateTime.Now;
            livrable.DeposeParId = userId;
            livrable.Commentaire = commentaire ?? string.Empty;
            livrable.Version = version ?? string.Empty;
            livrable.Phase = PhaseProjet.AnalyseClarification;
            livrable.TypeLivrable = TypeLivrable.CharteProjetSignee;

            projet.CharteProjet.SignatureSponsor = signatureSponsor;
            projet.CharteProjet.DateSignatureSponsor = signatureSponsor ? DateTime.Now : null;
            projet.CharteProjet.SignatureSponsorId = signatureSponsor ? projet.SponsorId : null;
            if (!signatureSponsor)
            {
                projet.CharteProjet.SignatureImageSponsor = null;
                projet.CharteProjet.DateSignatureImageSponsor = null;
            }

            projet.CharteProjet.SignatureChefProjet = signatureChefProjet;
            projet.CharteProjet.DateSignatureChefProjet = signatureChefProjet ? DateTime.Now : null;
            projet.CharteProjet.SignatureChefProjetId = signatureChefProjet ? projet.ChefProjetId : null;
            if (!signatureChefProjet)
            {
                projet.CharteProjet.SignatureImageCP = null;
                projet.CharteProjet.DateSignatureImageCP = null;
            }

            ResetCharteValidationState(projet);
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule;
            projet.CharteProjet.DateModification = DateTime.Now;
            projet.CharteProjet.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            if (dossierSignature != null)
            {
                var finalisation = await _electronicSignature.FinaliserDossierAsync(
                    dossierSignature.Id,
                    fichierSigne.FileName,
                    path,
                    _currentUserService.Matricule);

                if (!finalisation.Success)
                {
                    TempData["Error"] = $"Charte déposée, mais le dossier de signature n'a pas pu être finalisé : {finalisation.Message}";
                    return RedirectToAction(nameof(CharteProjet), new { id });
                }
            }

            await _auditService.LogActionAsync("UPLOAD_CHARTE_SIGNEE", "LivrableProjet", livrable.Id,
                null,
                new
                {
                    ProjetId = projet.Id,
                    livrable.NomDocument,
                    livrable.Version,
                    SignatureSponsor = projet.CharteProjet.SignatureSponsor,
                    SignatureChefProjet = projet.CharteProjet.SignatureChefProjet
                });

            TempData["Success"] = "Version signée de la charte déposée. Les validations DM/DSI ont été réinitialisées.";
            return RedirectToAction(nameof(CharteProjet), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> MettreAJourSignaturesCharte(
            Guid id,
            bool signatureSponsor = false,
            bool signatureChefProjet = false)
        {
            var projet = await _db.Projets
                .Include(p => p.CharteProjet)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (projet == null)
                return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanManageDossierSignature)
                return Forbid();

            if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            {
                TempData["Error"] = "La mise à jour des signatures est réservée à la phase Analyse.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            if (projet.CharteProjet == null)
            {
                TempData["Error"] = "La charte doit être initialisée avant de gérer les signatures.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            var hasChanged = false;

            if (projet.CharteProjet.SignatureSponsor != signatureSponsor)
            {
                projet.CharteProjet.SignatureSponsor = signatureSponsor;
                projet.CharteProjet.DateSignatureSponsor = signatureSponsor ? DateTime.Now : null;
                projet.CharteProjet.SignatureSponsorId = signatureSponsor ? projet.SponsorId : null;
                if (!signatureSponsor)
                {
                    projet.CharteProjet.SignatureImageSponsor = null;
                    projet.CharteProjet.DateSignatureImageSponsor = null;
                }

                hasChanged = true;
            }

            if (projet.CharteProjet.SignatureChefProjet != signatureChefProjet)
            {
                projet.CharteProjet.SignatureChefProjet = signatureChefProjet;
                projet.CharteProjet.DateSignatureChefProjet = signatureChefProjet ? DateTime.Now : null;
                projet.CharteProjet.SignatureChefProjetId = signatureChefProjet ? projet.ChefProjetId : null;
                if (!signatureChefProjet)
                {
                    projet.CharteProjet.SignatureImageCP = null;
                    projet.CharteProjet.DateSignatureImageCP = null;
                }

                hasChanged = true;
            }

            if (!hasChanged)
            {
                TempData["Info"] = "Aucun changement détecté sur les signatures de la charte.";
                return RedirectToAction(nameof(CharteProjet), new { id });
            }

            ResetCharteValidationState(projet);
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule;
            projet.CharteProjet.DateModification = DateTime.Now;
            projet.CharteProjet.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("MISE_A_JOUR_SIGNATURES_CHARTE", "CharteProjet", projet.CharteProjet.Id,
                null,
                new
                {
                    ProjetId = projet.Id,
                    SignatureSponsor = projet.CharteProjet.SignatureSponsor,
                    SignatureChefProjet = projet.CharteProjet.SignatureChefProjet
                });

            TempData["Success"] = "Signatures de la charte mises à jour. Les validations DM/DSI ont été réinitialisées.";
            return RedirectToAction(nameof(CharteProjet), new { id });
        }

        // POST: Valider Charte par DM
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DirecteurMetier,AdminIT")]
        public async Task<IActionResult> ValiderCharteDM(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.Sponsor)
                .Include(p => p.CharteProjet)
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();

            if (!await CanValidateCharteAsDirecteurMetierAsync(projet, userId))
                return Forbid();

            var result = await _charteWorkflow.ValiderDmAsync(id, userId);
            return MapCharteWorkflowToValidationsProjet(result);
        }

        // POST: Rejeter Charte par DM
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DirecteurMetier,AdminIT")]
        public async Task<IActionResult> RejeterCharteDM(Guid id, string commentaire)
        {
            var projet = await _db.Projets
                .Include(p => p.Sponsor)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();

            if (!await CanValidateCharteAsDirecteurMetierAsync(projet, userId))
                return Forbid();

            var result = await _charteWorkflow.RejeterDmAsync(id, commentaire);
            return MapCharteWorkflowToValidationsProjet(result);
        }

        // POST: Valider Charte par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DSI,ResponsableSolutionsIT,AdminIT")]
        public async Task<IActionResult> ValiderCharteDSI(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.CharteProjet)
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanValidateCharteAsDsiAsync(userId))
            {
                return Forbid();
            }

            var result = await _charteWorkflow.ValiderDsiAsync(id, userId);
            return MapCharteWorkflowToValidationsProjet(result);
        }

        // POST: Rejeter Charte par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DSI,ResponsableSolutionsIT,AdminIT")]
        public async Task<IActionResult> RejeterCharteDSI(Guid id, string commentaire)
        {
            var projet = await _db.Projets.FindAsync(id);

            if (projet == null)
                return NotFound();

            var userId = User.GetUserIdOrThrow();
            if (!await CanValidateCharteAsDsiAsync(userId))
            {
                return Forbid();
            }

            var result = await _charteWorkflow.RejeterDsiAsync(id, commentaire);
            return MapCharteWorkflowToValidationsProjet(result);
        }

        // POST: Annuler un dossier de signature électronique
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AnnulerDossierSignature(Guid id, Guid dossierSignatureId)
        {
            var projet = await _db.Projets.FindAsync(id);
            if (projet == null) return NotFound();

            var ui = await BuildProjectUiAsync(projet);
            if (!ui.CanManageDossierSignature)
                return Forbid();

            var dossier = await _db.DossiersSignatureProjets.FindAsync(dossierSignatureId);
            if (dossier == null || dossier.ProjetId != id)
                return NotFound();

            if (dossier.Statut == StatutDossierSignature.Signe)
            {
                TempData["Error"] = "Un dossier déjà signé ne peut pas être annulé.";
                return RedirectToAction(nameof(Details), new { id, tab = "analyse" });
            }

            dossier.Statut = StatutDossierSignature.Annule;
            dossier.DateModification = DateTime.Now;
            dossier.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("ANNULATION_DOSSIER_SIGNATURE", "DossierSignatureProjet", dossier.Id);

            TempData["Success"] = "Dossier de signature annulé.";
            return RedirectToAction(nameof(Details), new { id, tab = "analyse" });
        }

        // GET: Page de signature électronique de la charte
        [Authorize]
        public async Task<IActionResult> SignatureCharte(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.ChefProjet)
                .Include(p => p.Sponsor)
                .Include(p => p.CharteProjet)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet?.CharteProjet == null)
            {
                TempData["Error"] = "Charte projet introuvable.";
                return RedirectToAction(nameof(Details), new { id, tab = "analyse" });
            }

            var userId = User.GetUserIdOrThrow();

            string role;
            if (projet.ChefProjetId == userId || User.IsInRole("AdminIT"))
                role = "CP";
            else if (projet.SponsorId == userId || User.IsInRole("DirecteurMetier"))
                role = "Sponsor";
            else if (User.IsInRole("DSI") || User.IsInRole("ResponsableSolutionsIT"))
                role = "DSI";
            else
                return Forbid();

            return View(new SignatureCharteViewModel
            {
                Charte         = projet.CharteProjet,
                RoleSignataire = role,
                ProjetId       = id,
                NomSignataire  = $"{User.FindFirst("Nom")?.Value} {User.FindFirst("Prenoms")?.Value}".Trim()
            });
        }

        // POST: Enregistrer une signature électronique
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> EnregistrerSignature(Guid charteId, Guid projetId, string role, string signatureData)
        {
            if (string.IsNullOrWhiteSpace(signatureData) || !signatureData.StartsWith("data:image/png;base64,"))
            {
                TempData["Error"] = "Signature invalide.";
                return RedirectToAction(nameof(SignatureCharte), new { id = projetId });
            }

            var charte = await _db.CharteProjets.FindAsync(charteId);
            if (charte == null)
                return NotFound();

            var now = DateTime.Now;
            switch (role)
            {
                case "CP":
                    charte.SignatureImageCP = signatureData;
                    charte.DateSignatureImageCP = now;
                    charte.SignatureChefProjet = true;
                    charte.DateSignatureChefProjet = now;
                    break;
                case "Sponsor":
                    charte.SignatureImageSponsor = signatureData;
                    charte.DateSignatureImageSponsor = now;
                    charte.SignatureSponsor = true;
                    charte.DateSignatureSponsor = now;
                    break;
                case "DSI":
                    charte.SignatureImageDSI = signatureData;
                    charte.DateSignatureImageDSI = now;
                    break;
            }

            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync($"SIGNATURE_CHARTE_{role}", "CharteProjet", charteId);

            if (!string.IsNullOrWhiteSpace(charte.SignatureImageCP) &&
                !string.IsNullOrWhiteSpace(charte.SignatureImageSponsor) &&
                !string.IsNullOrWhiteSpace(charte.SignatureImageDSI))
            {
                var projet = await _db.Projets.FindAsync(charte.ProjetId);
                if (projet != null)
                {
                    projet.CharteValidee = true;
                    projet.DateCharteValidee = now;
                    await _db.SaveChangesAsync();
                }
                TempData["Success"] = "Signature enregistrée. Toutes les signatures sont complètes — charte validée !";
            }
            else
            {
                TempData["Success"] = "Signature enregistrée avec succès.";
            }

            return RedirectToAction(nameof(SignatureCharte), new { id = projetId });
        }

        private IActionResult MapCharteWorkflowToValidationsProjet(WorkflowResult result)
        {
            if (result.IsNotFound)
                return NotFound();

            if (result.IsForbidden)
                return Forbid();

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                TempData["Error"] = result.ErrorMessage;
            else if (!string.IsNullOrWhiteSpace(result.SuccessMessage))
                TempData["Success"] = result.SuccessMessage;

            return RedirectToAction(nameof(ValidationsProjet));
        }
    }
}
