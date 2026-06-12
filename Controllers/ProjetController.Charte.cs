using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.ViewModels.Projet;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Security;
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

            var charte = await _db.CharteProjets
                .Include(c => c.Demandeur)
                .Include(c => c.ChefProjet)
                .Include(c => c.SignatureSponsorUtilisateur)
                .Include(c => c.SignatureChefProjetUtilisateur)
                .Include(c => c.Jalons.Where(j => !j.EstSupprime).OrderBy(j => j.Ordre))
                .Include(c => c.PartiesPrenantes.Where(p => !p.EstSupprime))
                .FirstOrDefaultAsync(c => c.ProjetId == id && !c.EstSupprime);

            // Si la charte n'existe pas, créer une structure par défaut
            if (charte == null)
            {
                charte = new CharteProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projet.Id,
                    NomProjet = projet.Titre,
                    NumeroProjet = projet.CodeProjet,
                    ObjectifProjet = projet.Objectif ?? projet.DemandeProjet?.Objectifs ?? string.Empty,
                    DemandeurId = projet.DemandeProjet?.DemandeurId ?? Guid.Empty,
                    ChefProjetId = projet.ChefProjetId ?? Guid.Empty,
                    EmailChefProjet = projet.ChefProjet?.Email ?? string.Empty,
                    Sponsors = $"{projet.Sponsor?.Nom} {projet.Sponsor?.Prenoms}",
                    CodeDocument = $"CIT-CIV-DSI-CP-{projet.CodeProjet}-Rév.01",
                    TypeDocument = "Charte de projet",
                    Departement = "SYSTEME D'INFORMATION",
                    NumeroRevision = 1,
                    DateRevision = DateTime.Now,
                    DescriptionRevision = "Création",
                    DateCreation = DateTime.Now,
                    CreePar = _currentUserService.Matricule ?? "SYSTEM",
                    EstSupprime = false
                };

                // Ajouter les jalons par défaut
                var jalonsDefaut = new[]
                {
                    new { Nom = "Cahier de charge validé", Description = "Le document détaillé décrivant les besoins et les attentes du projet est officiellement approuvé par les parties prenantes.", Criteres = "Signature du responsable du projet et des parties prenantes, aucune objection majeure ou confirmation par mail." },
                    new { Nom = "Appel d'offre lancé", Description = "La phase d'appel d'offres est officiellement lancée, invitant les fournisseurs potentiels à soumettre leurs propositions.", Criteres = "Documentation complète de l'appel d'offres, diffusion auprès des soumissionnaires potentiels." },
                    new { Nom = "Soumissionnaire choisi", Description = "Un soumissionnaire est sélectionné après l'évaluation des propositions reçues.", Criteres = "Analyse comparative des soumissions, recommandation du comité de sélection, approbation du responsable du projet." },
                    new { Nom = "Infra IT Mise en Place", Description = "L'infrastructure nécessaire au projet est mise en place, comprenant les serveurs, les réseaux, et autres composants essentiels.", Criteres = "Vérification de la disponibilité et de la fonctionnalité de l'infrastructure selon les prérequis" },
                    new { Nom = "Paiement Fournisseur Réalisé", Description = "Le paiement convenu avec le fournisseur est effectué conformément aux termes du contrat", Criteres = "Confirmation de la transaction financière, documentation appropriée du paiement" },
                    new { Nom = "Solution Déployée", Description = "La solution logicielle est installée et configurée conformément aux spécifications du cahier de charge", Criteres = "Réussite des tests de déploiement, absence de problèmes critiques" },
                    new { Nom = "Utilisateurs Formés", Description = "Les utilisateurs concernés sont formés à l'utilisation de la nouvelle solution", Criteres = "Tous les utilisateurs ont suivi la formation, évaluation positive des compétences acquises" },
                    new { Nom = "Solution Utilisée", Description = "La solution est pleinement opérationnelle et utilisée par les utilisateurs finaux", Criteres = "Confirmation de l'utilisation régulière de la solution dans le cadre des activités quotidiennes" }
                };

                for (int i = 0; i < jalonsDefaut.Length; i++)
                {
                    charte.Jalons.Add(new JalonCharte
                    {
                        Id = Guid.NewGuid(),
                        CharteProjetId = charte.Id,
                        Nom = jalonsDefaut[i].Nom,
                        Description = jalonsDefaut[i].Description,
                        CriteresApprobation = jalonsDefaut[i].Criteres,
                        Ordre = i + 1,
                        DateCreation = DateTime.Now,
                        CreePar = _currentUserService.Matricule ?? "SYSTEM",
                        EstSupprime = false
                    });
                }

                _db.CharteProjets.Add(charte);
                await _db.SaveChangesAsync();
            }

            var utilisateurs = await _db.Utilisateurs
                .Where(u => !u.EstSupprime)
                .OrderBy(u => u.Nom)
                .ThenBy(u => u.Prenoms)
                .ToListAsync();
            var charteSigneeLivrable = await _db.LivrablesProjets
                .Include(l => l.DeposePar)
                .Where(l => l.ProjetId == id &&
                            l.TypeLivrable == TypeLivrable.CharteProjetSignee &&
                            !l.EstSupprime)
                .OrderByDescending(l => l.DateDepot)
                .FirstOrDefaultAsync();
            var dossierSignatureCharte = await _db.DossiersSignatureProjets
                .Include(d => d.Signataires.OrderBy(s => s.OrdreSignature))
                    .ThenInclude(s => s.Utilisateur)
                .Where(d => d.ProjetId == id &&
                            d.TypeDocument == TypeDocumentSignatureProjet.CharteProjet &&
                            !d.EstSupprime)
                .OrderByDescending(d => d.DateCreation)
                .FirstOrDefaultAsync();

            var vm = new CharteProjetPageViewModel
            {
                Charte                = charte,
                Projet                = projet,
                Utilisateurs          = utilisateurs,
                CharteSigneeLivrable  = charteSigneeLivrable,
                DossierSignatureCharte = dossierSignatureCharte
            };

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

            var userId = User.GetUserIdOrThrow();

            var charteExistante = await _db.CharteProjets
                .Include(c => c.Jalons)
                .Include(c => c.PartiesPrenantes)
                .FirstOrDefaultAsync(c => c.ProjetId == id);

            if (charteExistante == null)
            {
                charte.Id = Guid.NewGuid();
                charte.ProjetId = id;
                charte.DateCreation = DateTime.Now;
                charte.CreePar = _currentUserService.Matricule ?? "SYSTEM";
                charte.EstSupprime = false;

                // Ajouter les jalons
                if (Jalons != null)
                {
                    foreach (var jalon in Jalons.Where(j => !string.IsNullOrWhiteSpace(j.Nom)))
                    {
                        jalon.Id = jalon.Id == Guid.Empty ? Guid.NewGuid() : jalon.Id;
                        jalon.CharteProjetId = charte.Id;
                        jalon.DateCreation = DateTime.Now;
                        jalon.CreePar = _currentUserService.Matricule ?? "SYSTEM";
                        jalon.EstSupprime = false;
                        charte.Jalons.Add(jalon);
                    }
                }

                // Ajouter les parties prenantes
                if (PartiesPrenantes != null)
                {
                    foreach (var partie in PartiesPrenantes.Where(p => !string.IsNullOrWhiteSpace(p.Nom)))
                    {
                        partie.Id = partie.Id == Guid.Empty ? Guid.NewGuid() : partie.Id;
                        partie.CharteProjetId = charte.Id;
                        partie.DateCreation = DateTime.Now;
                        partie.CreePar = _currentUserService.Matricule ?? "SYSTEM";
                        partie.EstSupprime = false;
                        charte.PartiesPrenantes.Add(partie);
                    }
                }

                _db.CharteProjets.Add(charte);
            }
            else
            {
                // Mettre à jour les champs
                charteExistante.NomProjet = charte.NomProjet;
                charteExistante.NumeroProjet = charte.NumeroProjet;
                charteExistante.ObjectifProjet = charte.ObjectifProjet;
                charteExistante.AssuranceQualite = charte.AssuranceQualite;
                charteExistante.Perimetre = charte.Perimetre;
                charteExistante.ContraintesInitiales = charte.ContraintesInitiales;
                charteExistante.RisquesInitiaux = charte.RisquesInitiaux;
                charteExistante.Sponsors = charte.Sponsors;
                charteExistante.EmailChefProjet = charte.EmailChefProjet;
                charteExistante.CodeDocument = charte.CodeDocument;
                charteExistante.DescriptionRevision = charte.DescriptionRevision;
                charteExistante.RedigePar = charte.RedigePar;
                charteExistante.VerifiePar = charte.VerifiePar;
                charteExistante.ApprouvePar = charte.ApprouvePar;
                charteExistante.DemandeurId = charte.DemandeurId;
                charteExistante.ChefProjetId = charte.ChefProjetId;
                charteExistante.DateModification = DateTime.Now;
                charteExistante.ModifiePar = _currentUserService.Matricule;

                // Gérer les jalons
                if (Jalons != null)
                {
                    var jalonsIds = Jalons.Where(j => j.Id != Guid.Empty).Select(j => j.Id).ToList();
                    var jalonsASupprimer = charteExistante.Jalons.Where(j => !jalonsIds.Contains(j.Id)).ToList();
                    foreach (var jalon in jalonsASupprimer)
                    {
                        jalon.EstSupprime = true;
                        jalon.DateModification = DateTime.Now;
                        jalon.ModifiePar = _currentUserService.Matricule;
                    }

                    foreach (var jalon in Jalons.Where(j => !string.IsNullOrWhiteSpace(j.Nom)))
                    {
                        if (jalon.Id == Guid.Empty)
                        {
                            // Nouveau jalon
                            var nouveauJalon = new JalonCharte
                            {
                                Id = Guid.NewGuid(),
                                CharteProjetId = charteExistante.Id,
                                Nom = jalon.Nom,
                                Description = jalon.Description,
                                CriteresApprobation = jalon.CriteresApprobation,
                                DatePrevisionnelle = jalon.DatePrevisionnelle,
                                Ordre = jalon.Ordre,
                                DateCreation = DateTime.Now,
                                CreePar = _currentUserService.Matricule ?? "SYSTEM",
                                EstSupprime = false
                            };
                            charteExistante.Jalons.Add(nouveauJalon);
                        }
                        else
                        {
                            // Mettre à jour jalon existant
                            var jalonExistant = charteExistante.Jalons.FirstOrDefault(j => j.Id == jalon.Id);
                            if (jalonExistant != null)
                            {
                                jalonExistant.Nom = jalon.Nom;
                                jalonExistant.Description = jalon.Description;
                                jalonExistant.CriteresApprobation = jalon.CriteresApprobation;
                                jalonExistant.DatePrevisionnelle = jalon.DatePrevisionnelle;
                                jalonExistant.Ordre = jalon.Ordre;
                                jalonExistant.DateModification = DateTime.Now;
                                jalonExistant.ModifiePar = _currentUserService.Matricule;
                            }
                        }
                    }
                }

                // Gérer les parties prenantes
                if (PartiesPrenantes != null)
                {
                    var partiesIds = PartiesPrenantes.Where(p => p.Id != Guid.Empty).Select(p => p.Id).ToList();
                    var partiesASupprimer = charteExistante.PartiesPrenantes.Where(p => !partiesIds.Contains(p.Id)).ToList();
                    foreach (var partie in partiesASupprimer)
                    {
                        partie.EstSupprime = true;
                        partie.DateModification = DateTime.Now;
                        partie.ModifiePar = _currentUserService.Matricule;
                    }

                    foreach (var partie in PartiesPrenantes.Where(p => !string.IsNullOrWhiteSpace(p.Nom)))
                    {
                        if (partie.Id == Guid.Empty)
                        {
                            // Nouvelle partie prenante
                            var nouvellePartie = new PartiePrenanteCharte
                            {
                                Id = Guid.NewGuid(),
                                CharteProjetId = charteExistante.Id,
                                Nom = partie.Nom,
                                Role = partie.Role,
                                UtilisateurId = partie.UtilisateurId,
                                DateCreation = DateTime.Now,
                                CreePar = _currentUserService.Matricule ?? "SYSTEM",
                                EstSupprime = false
                            };
                            charteExistante.PartiesPrenantes.Add(nouvellePartie);
                        }
                        else
                        {
                            // Mettre à jour partie prenante existante
                            var partieExistante = charteExistante.PartiesPrenantes.FirstOrDefault(p => p.Id == partie.Id);
                            if (partieExistante != null)
                            {
                                partieExistante.Nom = partie.Nom;
                                partieExistante.Role = partie.Role;
                                partieExistante.UtilisateurId = partie.UtilisateurId;
                                partieExistante.DateModification = DateTime.Now;
                                partieExistante.ModifiePar = _currentUserService.Matricule;
                            }
                        }
                    }
                }
            }

            await _db.SaveChangesAsync();

            var workflowReset = false;
            if (projet.PhaseActuelle == PhaseProjet.AnalyseClarification)
            {
                var charteActive = charteExistante ?? charte;
                var signedLivrables = await _db.LivrablesProjets
                    .Where(l => l.ProjetId == id &&
                                l.TypeLivrable == TypeLivrable.CharteProjetSignee &&
                                !l.EstSupprime)
                    .ToListAsync();

                if (signedLivrables.Any() ||
                    charteActive.SignatureSponsor ||
                    charteActive.SignatureChefProjet ||
                    projet.CharteValidee ||
                    projet.CharteValideeParDM ||
                    projet.CharteValideeParDSI)
                {
                    var dossierSignature = await _db.DossiersSignatureProjets
                        .Include(d => d.Signataires)
                        .FirstOrDefaultAsync(d => d.ProjetId == id &&
                                                  d.TypeDocument == TypeDocumentSignatureProjet.CharteProjet &&
                                                  !d.EstSupprime);

                    foreach (var livrableSigne in signedLivrables)
                    {
                        livrableSigne.EstSupprime = true;
                        livrableSigne.DateModification = DateTime.Now;
                        livrableSigne.ModifiePar = _currentUserService.Matricule;
                    }

                    charteActive.SignatureSponsor = false;
                    charteActive.DateSignatureSponsor = null;
                    charteActive.SignatureSponsorId = null;
                    charteActive.SignatureImageSponsor = null;
                    charteActive.DateSignatureImageSponsor = null;
                    charteActive.SignatureChefProjet = false;
                    charteActive.DateSignatureChefProjet = null;
                    charteActive.SignatureChefProjetId = null;
                    charteActive.SignatureImageCP = null;
                    charteActive.DateSignatureImageCP = null;
                    charteActive.DateModification = DateTime.Now;
                    charteActive.ModifiePar = _currentUserService.Matricule;

                    if (dossierSignature != null)
                    {
                        dossierSignature.Statut = StatutDossierSignature.Brouillon;
                        dossierSignature.NomDocumentSigne = null;
                        dossierSignature.CheminDocumentSigne = null;
                        dossierSignature.DateEnvoi = null;
                        dossierSignature.DateFinalisation = null;
                        dossierSignature.DateExpiration = null;
                        dossierSignature.MessageStatut = "La charte a ete modifiee. Le circuit de signature doit etre relance.";
                        dossierSignature.DateModification = DateTime.Now;
                        dossierSignature.ModifiePar = _currentUserService.Matricule;

                        foreach (var signataire in dossierSignature.Signataires)
                        {
                            signataire.Statut = StatutSignataireDossierSignature.EnAttente;
                            signataire.DateSignature = null;
                            signataire.DateModification = DateTime.Now;
                            signataire.ModifiePar = _currentUserService.Matricule;
                        }
                    }

                    ResetCharteValidationState(projet);
                    projet.DateModification = DateTime.Now;
                    projet.ModifiePar = _currentUserService.Matricule;

                    await _db.SaveChangesAsync();
                    workflowReset = true;
                }
            }

            await _auditService.LogActionAsync("SAUVEGARDE_CHARTE_PROJET", "CharteProjet", charteExistante?.Id ?? charte.Id);

            TempData["Success"] = workflowReset
                ? "Charte de projet sauvegardée. La version signée et les validations ont été réinitialisées."
                : "Charte de projet sauvegardée avec succès.";
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
        [Authorize(Roles = "DirecteurMetier")]
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

            if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            {
                TempData["Error"] = "La charte ne peut être validée qu'en phase Analyse.";
                return RedirectToAction(nameof(ValidationsProjet));
            }

            if (!HasCompleteSignedCharte(projet))
            {
                TempData["Error"] = "La charte signée complète doit être déposée avant la validation DM.";
                return RedirectToAction(nameof(ValidationsProjet));
            }

            projet.CharteValideeParDM = true;
            projet.DateCharteValideeParDM = DateTime.Now;
            projet.CharteValideeParDMId = userId;
            projet.CommentaireRefusCharteDM = null;
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule;

            // Si DM et DSI ont validé, la charte est complètement validée
            if (projet.CharteValideeParDM && projet.CharteValideeParDSI)
            {
                projet.CharteValidee = true;
                projet.DateCharteValidee = DateTime.Now;
            }

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_CHARTE_DM", "Projet", projet.Id);

            TempData["Success"] = "Charte validée par le Directeur Métier.";
            return RedirectToAction(nameof(ValidationsProjet));
        }

        // POST: Rejeter Charte par DM
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DirecteurMetier")]
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

            if (string.IsNullOrWhiteSpace(commentaire))
            {
                TempData["Error"] = "Le commentaire est obligatoire pour rejeter la charte.";
                return RedirectToAction(nameof(ValidationsProjet));
            }

            projet.CharteValideeParDM = false;
            projet.DateCharteValideeParDM = null;
            projet.CharteValideeParDMId = null;
            projet.CommentaireRefusCharteDM = commentaire.Trim();
            projet.CharteValidee = false;
            projet.DateCharteValidee = null;
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("REJET_CHARTE_DM", "Projet", projet.Id,
                new { Commentaire = commentaire });

            TempData["Success"] = "Charte rejetée par le Directeur Métier.";
            return RedirectToAction(nameof(ValidationsProjet));
        }

        // POST: Valider Charte par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DSI,ResponsableSolutionsIT")]
        public async Task<IActionResult> ValiderCharteDSI(Guid id)
        {
            var projet = await _db.Projets
                .Include(p => p.CharteProjet)
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            {
                TempData["Error"] = "La charte ne peut être validée qu'en phase Analyse.";
                return RedirectToAction(nameof(ValidationsProjet));
            }

            var userId = User.GetUserIdOrThrow();
            if (!await CanValidateCharteAsDsiAsync(userId))
            {
                return Forbid();
            }

            if (!projet.CharteValideeParDM)
            {
                TempData["Error"] = "La charte doit d'abord être validée par le Directeur Métier.";
                return RedirectToAction(nameof(ValidationsProjet));
            }

            if (!HasCompleteSignedCharte(projet))
            {
                TempData["Error"] = "La charte signée complète doit être déposée avant la validation DSI.";
                return RedirectToAction(nameof(ValidationsProjet));
            }

            projet.CharteValideeParDSI = true;
            projet.DateCharteValideeParDSI = DateTime.Now;
            projet.CharteValideeParDSIId = userId;
            projet.CommentaireRefusCharteDSI = null;
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule;

            // Si DM et DSI ont validé, la charte est complètement validée
            if (projet.CharteValideeParDM && projet.CharteValideeParDSI)
            {
                projet.CharteValidee = true;
                projet.DateCharteValidee = DateTime.Now;
            }

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("VALIDATION_CHARTE_DSI", "Projet", projet.Id);

            TempData["Success"] = "Charte validée par la DSI.";
            return RedirectToAction(nameof(ValidationsProjet));
        }

        // POST: Rejeter Charte par DSI
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DSI,ResponsableSolutionsIT")]
        public async Task<IActionResult> RejeterCharteDSI(Guid id, string commentaire)
        {
            var projet = await _db.Projets.FindAsync(id);

            if (projet == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(commentaire))
            {
                TempData["Error"] = "Le commentaire est obligatoire pour rejeter la charte.";
                return RedirectToAction(nameof(ValidationsProjet));
            }

            var userId = User.GetUserIdOrThrow();
            if (!await CanValidateCharteAsDsiAsync(userId))
            {
                return Forbid();
            }

            projet.CharteValideeParDSI = false;
            projet.DateCharteValideeParDSI = null;
            projet.CharteValideeParDSIId = null;
            projet.CommentaireRefusCharteDSI = commentaire.Trim();
            projet.CharteValidee = false;
            projet.DateCharteValidee = null;
            projet.DateModification = DateTime.Now;
            projet.ModifiePar = _currentUserService.Matricule;

            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("REJET_CHARTE_DSI", "Projet", projet.Id,
                new { Commentaire = commentaire });

            TempData["Success"] = "Charte rejetée par la DSI.";
            return RedirectToAction(nameof(ValidationsProjet));
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
    }
}
