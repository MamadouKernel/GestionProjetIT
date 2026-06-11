using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    public class ElectronicSignatureService : IElectronicSignatureService
    {
        private readonly ApplicationDbContext _context;

        public ElectronicSignatureService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DossierSignatureProjet> InitialiserCharteAsync(Guid projetId, FournisseurSignatureElectronique fournisseur, string? currentUserMatricule)
        {
            var projet = await _context.Projets
                .Include(p => p.DemandeProjet)
                    .ThenInclude(d => d.Demandeur)
                .Include(p => p.Sponsor)
                .Include(p => p.ChefProjet)
                .Include(p => p.Livrables)
                .FirstOrDefaultAsync(p => p.Id == projetId);

            if (projet == null)
                throw new InvalidOperationException("Projet introuvable.");

            var dossier = await _context.Set<DossierSignatureProjet>()
                .Include(d => d.Signataires)
                .FirstOrDefaultAsync(d => d.ProjetId == projetId && d.TypeDocument == TypeDocumentSignatureProjet.CharteProjet);

            var livrableSource = projet.Livrables
                .Where(l => l.TypeLivrable == TypeLivrable.CharteProjet)
                .OrderByDescending(l => l.DateDepot)
                .FirstOrDefault();

            if (dossier == null)
            {
                dossier = new DossierSignatureProjet
                {
                    Id = Guid.NewGuid(),
                    ProjetId = projetId,
                    TypeDocument = TypeDocumentSignatureProjet.CharteProjet,
                    DateCreation = DateTime.Now,
                    CreePar = currentUserMatricule ?? "SYSTEM"
                };
                _context.Add(dossier);
            }

            dossier.Fournisseur = fournisseur;
            dossier.Statut = StatutDossierSignature.Brouillon;
            dossier.LivrableSourceId = livrableSource?.Id;
            dossier.NomDocumentSource = livrableSource?.NomDocument ?? $"CharteProjet_{projet.CodeProjet}.pdf";
            dossier.CheminDocumentSource = livrableSource?.CheminRelatif;
            dossier.NomDocumentSigne = null;
            dossier.CheminDocumentSigne = null;
            dossier.ExternalRequestId = null;
            dossier.UrlSuivi = null;
            dossier.DateEnvoi = null;
            dossier.DateFinalisation = null;
            dossier.DateExpiration = null;
            dossier.MessageStatut = livrableSource == null
                ? "Dossier initialise. Generez d'abord la charte PDF du projet avant l'envoi a la signature."
                : "Dossier initialise. Verifiez les signataires puis envoyez la demande de signature.";
            dossier.DateModification = DateTime.Now;
            dossier.ModifiePar = currentUserMatricule;

            SynchroniserSignataires(dossier, projet, currentUserMatricule);
            await _context.SaveChangesAsync();
            return dossier;
        }

        public async Task<SignatureOperationResult> EnvoyerDossierAsync(Guid dossierId, string? currentUserMatricule)
        {
            var dossier = await ChargerDossierAsync(dossierId);
            if (dossier == null)
                return Echec("Dossier de signature introuvable.");

            if (string.IsNullOrWhiteSpace(dossier.CheminDocumentSource))
                return Echec("Generez d'abord la charte PDF source avant l'envoi a la signature.");

            if (!dossier.Signataires.Any())
                return Echec("Aucun signataire n'est defini pour ce dossier.");

            if (dossier.Signataires.Any(s => string.IsNullOrWhiteSpace(s.Email)))
                return Echec("Tous les signataires doivent disposer d'une adresse email avant l'envoi.");

            dossier.ExternalRequestId ??= GenererReferenceExterne(dossier.Fournisseur);
            dossier.UrlSuivi ??= ConstruireUrlSuivi(dossier.Fournisseur, dossier.ExternalRequestId);
            dossier.DateEnvoi = DateTime.Now;
            dossier.DateExpiration = DateTime.Now.AddDays(14);
            dossier.Statut = StatutDossierSignature.EnCoursSignature;
            dossier.MessageStatut = $"Demande envoyee via {dossier.Fournisseur}. Suivi externe: {dossier.ExternalRequestId}.";
            dossier.DateModification = DateTime.Now;
            dossier.ModifiePar = currentUserMatricule;

            await _context.SaveChangesAsync();
            return Succes(dossier, dossier.MessageStatut);
        }

        public async Task<SignatureOperationResult> EnregistrerDecisionSignataireAsync(Guid dossierId, Guid signataireId, bool approuver, string? currentUserMatricule)
        {
            var dossier = await ChargerDossierAsync(dossierId);
            if (dossier == null)
                return Echec("Dossier de signature introuvable.");

            var signataire = dossier.Signataires.FirstOrDefault(s => s.Id == signataireId);
            if (signataire == null)
                return Echec("Signataire introuvable.");

            signataire.Statut = approuver ? StatutSignataireDossierSignature.Signe : StatutSignataireDossierSignature.Refuse;
            signataire.DateSignature = DateTime.Now;
            signataire.DateModification = DateTime.Now;
            signataire.ModifiePar = currentUserMatricule;

            if (!approuver)
            {
                dossier.Statut = StatutDossierSignature.Refuse;
                dossier.MessageStatut = $"Le signataire {signataire.NomComplet} a refuse la demande de signature.";
            }
            else if (dossier.Signataires.All(s => s.Statut == StatutSignataireDossierSignature.Signe))
            {
                dossier.Statut = StatutDossierSignature.Signe;
                dossier.DateFinalisation = DateTime.Now;
                dossier.MessageStatut = "Tous les signataires ont approuve la demande. Le document signe peut etre verse au dossier projet.";
            }
            else
            {
                dossier.Statut = StatutDossierSignature.EnCoursSignature;
                dossier.MessageStatut = $"Signature enregistree pour {signataire.NomComplet}.";
            }

            dossier.DateModification = DateTime.Now;
            dossier.ModifiePar = currentUserMatricule;

            await _context.SaveChangesAsync();
            return Succes(dossier, dossier.MessageStatut);
        }

        public async Task<SignatureOperationResult> FinaliserDossierAsync(Guid dossierId, string nomDocumentSigne, string cheminDocumentSigne, string? currentUserMatricule)
        {
            var dossier = await ChargerDossierAsync(dossierId);
            if (dossier == null)
                return Echec("Dossier de signature introuvable.");

            if (dossier.Signataires.Any(s => s.Statut != StatutSignataireDossierSignature.Signe))
                return Echec("Toutes les signatures doivent etre confirmees avant le versement du document signe.");

            dossier.NomDocumentSigne = nomDocumentSigne;
            dossier.CheminDocumentSigne = cheminDocumentSigne;
            dossier.Statut = StatutDossierSignature.Signe;
            dossier.DateFinalisation ??= DateTime.Now;
            dossier.MessageStatut = "Document signe verse au dossier du projet.";
            dossier.DateModification = DateTime.Now;
            dossier.ModifiePar = currentUserMatricule;

            await _context.SaveChangesAsync();
            return Succes(dossier, dossier.MessageStatut);
        }

        private async Task<DossierSignatureProjet?> ChargerDossierAsync(Guid dossierId)
        {
            return await _context.Set<DossierSignatureProjet>()
                .Include(d => d.Signataires)
                .FirstOrDefaultAsync(d => d.Id == dossierId);
        }

        private void SynchroniserSignataires(DossierSignatureProjet dossier, Projet projet, string? currentUserMatricule)
        {
            var definitions = new[]
            {
                new
                {
                    Role = RoleSignataireProjet.Sponsor,
                    Ordre = 1,
                    Utilisateur = (Utilisateur?)projet.Sponsor,
                    Nom = $"{projet.Sponsor?.Nom} {projet.Sponsor?.Prenoms}".Trim(),
                    Email = projet.Sponsor?.Email ?? string.Empty
                },
                new
                {
                    Role = RoleSignataireProjet.ChefDeProjet,
                    Ordre = 2,
                    Utilisateur = (Utilisateur?)projet.ChefProjet,
                    Nom = $"{projet.ChefProjet?.Nom} {projet.ChefProjet?.Prenoms}".Trim(),
                    Email = projet.ChefProjet?.Email ?? string.Empty
                }
            };

            foreach (var definition in definitions)
            {
                var signataire = dossier.Signataires.FirstOrDefault(s => s.Role == definition.Role);
                if (signataire == null)
                {
                    signataire = new SignataireDossierSignatureProjet
                    {
                        Id = Guid.NewGuid(),
                        DossierSignatureProjetId = dossier.Id,
                        DateCreation = DateTime.Now,
                        CreePar = currentUserMatricule ?? "SYSTEM"
                    };
                    dossier.Signataires.Add(signataire);
                }

                signataire.UtilisateurId = definition.Utilisateur?.Id;
                signataire.NomComplet = definition.Nom;
                signataire.Email = definition.Email;
                signataire.Role = definition.Role;
                signataire.OrdreSignature = definition.Ordre;
                signataire.Statut = StatutSignataireDossierSignature.EnAttente;
                signataire.DateSignature = null;
                signataire.DateModification = DateTime.Now;
                signataire.ModifiePar = currentUserMatricule;
            }
        }

        private static string GenererReferenceExterne(FournisseurSignatureElectronique fournisseur)
        {
            var prefix = fournisseur switch
            {
                FournisseurSignatureElectronique.DocuSign => "DOCUSIGN",
                FournisseurSignatureElectronique.AdobeSign => "ADOBE",
                _ => "MANUEL"
            };

            return $"{prefix}-{DateTime.Now:yyyyMMddHHmmss}";
        }

        private static string ConstruireUrlSuivi(FournisseurSignatureElectronique fournisseur, string? externalRequestId)
        {
            var baseUrl = fournisseur switch
            {
                FournisseurSignatureElectronique.DocuSign => "https://apps.docusign.com",
                FournisseurSignatureElectronique.AdobeSign => "https://secure.na1.echosign.com",
                _ => "/Projet/CharteProjet"
            };

            return string.IsNullOrWhiteSpace(externalRequestId)
                ? baseUrl
                : $"{baseUrl}?requestId={Uri.EscapeDataString(externalRequestId)}";
        }

        private static SignatureOperationResult Succes(DossierSignatureProjet dossier, string message)
        {
            return new SignatureOperationResult
            {
                Success = true,
                Message = message,
                Dossier = dossier
            };
        }

        private static SignatureOperationResult Echec(string message)
        {
            return new SignatureOperationResult
            {
                Success = false,
                Message = message
            };
        }
    }
}
