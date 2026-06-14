using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Application.ViewModels.Projet;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public class CharteProjetWorkflowService : ICharteProjetWorkflowService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    public CharteProjetWorkflowService(
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _db = db;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    /// <summary>
    /// Charge la charte du projet pour affichage/édition. Crée une charte par défaut
    /// (avec ses jalons standard) si elle n'existe pas encore, puis assemble le
    /// view-model de la page (utilisateurs, dernier livrable signé, dossier de
    /// signature). L'autorisation reste au contrôleur.
    /// </summary>
    public async Task<CharteProjetPageViewModel> ObtenirPourAffichageAsync(Projet projet)
    {
        var id = projet.Id;

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

        return new CharteProjetPageViewModel
        {
            Charte                = charte,
            Projet                = projet,
            Utilisateurs          = utilisateurs,
            CharteSigneeLivrable  = charteSigneeLivrable,
            DossierSignatureCharte = dossierSignatureCharte
        };
    }

    public async Task<WorkflowResult> ValiderDmAsync(Guid projetId, Guid userId)
    {
        var projet = await _db.Projets
            .Include(p => p.CharteProjet)
            .Include(p => p.Livrables)
            .FirstOrDefaultAsync(p => p.Id == projetId);

        if (projet == null)
            return WorkflowResult.NotFound();

        if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            return WorkflowResult.Error("La charte ne peut être validée qu'en phase Analyse.");

        if (!HasCompleteSignedCharte(projet))
            return WorkflowResult.Error("La charte signée complète doit être déposée avant la validation DM.");

        projet.CharteValideeParDM = true;
        projet.DateCharteValideeParDM = DateTime.Now;
        projet.CharteValideeParDMId = userId;
        projet.CommentaireRefusCharteDM = null;
        projet.DateModification = DateTime.Now;
        projet.ModifiePar = _currentUserService.Matricule;

        if (projet.CharteValideeParDM && projet.CharteValideeParDSI)
        {
            projet.CharteValidee = true;
            projet.DateCharteValidee = DateTime.Now;
        }

        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync("VALIDATION_CHARTE_DM", "Projet", projet.Id);

        return WorkflowResult.Success("Charte validée par le Directeur Métier.");
    }

    public async Task<WorkflowResult> RejeterDmAsync(Guid projetId, string commentaire)
    {
        var projet = await _db.Projets.FindAsync(projetId);

        if (projet == null)
            return WorkflowResult.NotFound();

        if (string.IsNullOrWhiteSpace(commentaire))
            return WorkflowResult.Error("Le commentaire est obligatoire pour rejeter la charte.");

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

        return WorkflowResult.Success("Charte rejetée par le Directeur Métier.");
    }

    public async Task<WorkflowResult> ValiderDsiAsync(Guid projetId, Guid userId)
    {
        var projet = await _db.Projets
            .Include(p => p.CharteProjet)
            .Include(p => p.Livrables)
            .FirstOrDefaultAsync(p => p.Id == projetId);

        if (projet == null)
            return WorkflowResult.NotFound();

        if (projet.PhaseActuelle != PhaseProjet.AnalyseClarification)
            return WorkflowResult.Error("La charte ne peut être validée qu'en phase Analyse.");

        if (!projet.CharteValideeParDM)
            return WorkflowResult.Error("La charte doit d'abord être validée par le Directeur Métier.");

        if (!HasCompleteSignedCharte(projet))
            return WorkflowResult.Error("La charte signée complète doit être déposée avant la validation DSI.");

        projet.CharteValideeParDSI = true;
        projet.DateCharteValideeParDSI = DateTime.Now;
        projet.CharteValideeParDSIId = userId;
        projet.CommentaireRefusCharteDSI = null;
        projet.DateModification = DateTime.Now;
        projet.ModifiePar = _currentUserService.Matricule;

        if (projet.CharteValideeParDM && projet.CharteValideeParDSI)
        {
            projet.CharteValidee = true;
            projet.DateCharteValidee = DateTime.Now;
        }

        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync("VALIDATION_CHARTE_DSI", "Projet", projet.Id);

        return WorkflowResult.Success("Charte validée par la DSI.");
    }

    public async Task<WorkflowResult> RejeterDsiAsync(Guid projetId, string commentaire)
    {
        var projet = await _db.Projets.FindAsync(projetId);

        if (projet == null)
            return WorkflowResult.NotFound();

        if (string.IsNullOrWhiteSpace(commentaire))
            return WorkflowResult.Error("Le commentaire est obligatoire pour rejeter la charte.");

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

        return WorkflowResult.Success("Charte rejetée par la DSI.");
    }

    public async Task<WorkflowResult> SauvegarderAsync(
        Guid projetId,
        CharteProjet charte,
        List<JalonCharte>? jalons,
        List<PartiePrenanteCharte>? partiesPrenantes,
        Guid userId)
    {
        var projet = await _db.Projets.FindAsync(projetId);
        if (projet == null)
            return WorkflowResult.NotFound();

        var charteExistante = await _db.CharteProjets
            .Include(c => c.Jalons)
            .Include(c => c.PartiesPrenantes)
            .FirstOrDefaultAsync(c => c.ProjetId == projetId);

        if (charteExistante == null)
        {
            charte.Id = Guid.NewGuid();
            charte.ProjetId = projetId;
            charte.DateCreation = DateTime.Now;
            charte.CreePar = _currentUserService.Matricule ?? "SYSTEM";
            charte.EstSupprime = false;

            if (jalons != null)
            {
                foreach (var jalon in jalons.Where(j => !string.IsNullOrWhiteSpace(j.Nom)))
                {
                    jalon.Id = jalon.Id == Guid.Empty ? Guid.NewGuid() : jalon.Id;
                    jalon.CharteProjetId = charte.Id;
                    jalon.DateCreation = DateTime.Now;
                    jalon.CreePar = _currentUserService.Matricule ?? "SYSTEM";
                    jalon.EstSupprime = false;
                    charte.Jalons.Add(jalon);
                }
            }

            if (partiesPrenantes != null)
            {
                foreach (var partie in partiesPrenantes.Where(p => !string.IsNullOrWhiteSpace(p.Nom)))
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

            if (jalons != null)
            {
                var jalonsIds = jalons.Where(j => j.Id != Guid.Empty).Select(j => j.Id).ToList();
                var jalonsASupprimer = charteExistante.Jalons.Where(j => !jalonsIds.Contains(j.Id)).ToList();
                foreach (var jalon in jalonsASupprimer)
                {
                    jalon.EstSupprime = true;
                    jalon.DateModification = DateTime.Now;
                    jalon.ModifiePar = _currentUserService.Matricule;
                }

                foreach (var jalon in jalons.Where(j => !string.IsNullOrWhiteSpace(j.Nom)))
                {
                    if (jalon.Id == Guid.Empty)
                    {
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

            if (partiesPrenantes != null)
            {
                var partiesIds = partiesPrenantes.Where(p => p.Id != Guid.Empty).Select(p => p.Id).ToList();
                var partiesASupprimer = charteExistante.PartiesPrenantes.Where(p => !partiesIds.Contains(p.Id)).ToList();
                foreach (var partie in partiesASupprimer)
                {
                    partie.EstSupprime = true;
                    partie.DateModification = DateTime.Now;
                    partie.ModifiePar = _currentUserService.Matricule;
                }

                foreach (var partie in partiesPrenantes.Where(p => !string.IsNullOrWhiteSpace(p.Nom)))
                {
                    if (partie.Id == Guid.Empty)
                    {
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
                .Where(l => l.ProjetId == projetId &&
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
                    .FirstOrDefaultAsync(d => d.ProjetId == projetId &&
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

        return WorkflowResult.Success(workflowReset
            ? "Charte de projet sauvegardée. La version signée et les validations ont été réinitialisées."
            : "Charte de projet sauvegardée avec succès.");
    }

    private static void ResetCharteValidationState(Projet projet)
    {
        projet.CharteValideeParDM = false;
        projet.DateCharteValideeParDM = null;
        projet.CharteValideeParDMId = null;
        projet.CommentaireRefusCharteDM = null;

        projet.CharteValideeParDSI = false;
        projet.DateCharteValideeParDSI = null;
        projet.CharteValideeParDSIId = null;
        projet.CommentaireRefusCharteDSI = null;

        projet.CharteValidee = false;
        projet.DateCharteValidee = null;
    }

    private static bool HasCompleteSignedCharte(Projet projet)
    {
        var hasSignedLivrable = projet.Livrables.Any(l =>
            !l.EstSupprime &&
            l.TypeLivrable == TypeLivrable.CharteProjetSignee);

        return hasSignedLivrable &&
               projet.CharteProjet?.SignatureSponsor == true &&
               projet.CharteProjet?.SignatureChefProjet == true;
    }
}
