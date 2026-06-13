using GestionProjects.Application.Common.Interfaces;
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
