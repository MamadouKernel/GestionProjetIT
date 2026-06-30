using GestionProjects.Application.Common.Constants;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Domain.Enums;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services
{
    /// <summary>
    /// Rappels automatiques scriptés (sans LLM) : charges hebdomadaires non saisies,
    /// bénéfices arrivés à échéance d'évaluation, et suggestion d'avenant en cas de
    /// dérive budget/délai significative. Tourne une fois par jour.
    ///
    /// Pour limiter le bruit sans ajouter de colonne "dernier rappel envoyé" :
    /// - charges : rappel uniquement le jeudi (laisse le temps de saisir avant la fin de semaine) ;
    /// - bénéfices : rappel le jour exact de l'échéance (DateCibleRealisation == aujourd'hui), donc
    ///   ne se répète pas tant que la date ne change pas ;
    /// - avenant : suggestion uniquement le lundi.
    /// </summary>
    public class RappelsAutomatiquesBackgroundService : BackgroundService
    {
        private static readonly TimeSpan IntervalleVerification = TimeSpan.FromHours(24);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RappelsAutomatiquesBackgroundService> _logger;

        public RappelsAutomatiquesBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<RappelsAutomatiquesBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExecuterRappelsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur lors de l'exécution des rappels automatiques.");
                }

                try
                {
                    await Task.Delay(IntervalleVerification, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Arrêt normal de l'application.
                }
            }
        }

        private async Task ExecuterRappelsAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var aujourdhui = DateTime.Today;

            if (aujourdhui.DayOfWeek == DayOfWeek.Thursday)
            {
                await RappellerChargesManquantesAsync(db, notifications, aujourdhui, cancellationToken);
            }

            await RappellerBeneficesAEvaluerAsync(db, notifications, aujourdhui, cancellationToken);

            if (aujourdhui.DayOfWeek == DayOfWeek.Monday)
            {
                await SuggererAvenantsAsync(db, notifications, cancellationToken);
            }
        }

        private static async Task RappellerChargesManquantesAsync(
            ApplicationDbContext db, INotificationService notifications, DateTime aujourdhui, CancellationToken ct)
        {
            var lundiSemaineCourante = aujourdhui.AddDays(-(int)aujourdhui.DayOfWeek + (int)DayOfWeek.Monday).Date;
            if (lundiSemaineCourante > aujourdhui)
            {
                lundiSemaineCourante = lundiSemaineCourante.AddDays(-7);
            }

            var projetsActifs = await db.Projets
                .Where(p => p.StatutProjet == StatutProjet.EnCours)
                .Select(p => new { p.Id, p.CodeProjet, p.Titre })
                .ToListAsync(ct);

            foreach (var projet in projetsActifs)
            {
                var ressourcesSuivies = await db.ChargesProjets
                    .Where(c => c.ProjetId == projet.Id && !c.EstSupprime)
                    .Select(c => c.RessourceId)
                    .Distinct()
                    .ToListAsync(ct);

                foreach (var ressourceId in ressourcesSuivies)
                {
                    var aDejaSaisi = await db.ChargesProjets.AnyAsync(c =>
                        c.ProjetId == projet.Id &&
                        c.RessourceId == ressourceId &&
                        !c.EstSupprime &&
                        c.SemaineDebut.Date == lundiSemaineCourante, ct);

                    if (!aDejaSaisi)
                    {
                        await notifications.NotifierUtilisateurAsync(
                            ressourceId,
                            TypeNotification.ChargeHebdomadaireManquante,
                            $"Charge hebdomadaire non saisie - {projet.CodeProjet}",
                            $"Vous n'avez pas encore saisi votre charge de la semaine du {lundiSemaineCourante:dd/MM/yyyy} sur le projet \"{projet.Titre}\".",
                            DomainEntityTypes.ChargeProjet,
                            projet.Id);
                    }
                }
            }
        }

        private static async Task RappellerBeneficesAEvaluerAsync(
            ApplicationDbContext db, INotificationService notifications, DateTime aujourdhui, CancellationToken ct)
        {
            var beneficesAEvaluer = await db.BeneficesProjets
                .Include(b => b.Projet)
                .Where(b => !b.EstSupprime &&
                            b.Statut == StatutBenefice.Attendu &&
                            b.DateCibleRealisation.HasValue &&
                            b.DateCibleRealisation.Value.Date == aujourdhui)
                .ToListAsync(ct);

            foreach (var benefice in beneficesAEvaluer)
            {
                var destinataire = benefice.Projet.ChefProjetId ?? benefice.Projet.SponsorId;
                await notifications.NotifierUtilisateurAsync(
                    destinataire,
                    TypeNotification.BeneficeAEvaluer,
                    $"Bénéfice à évaluer - {benefice.Projet.CodeProjet}",
                    $"Le bénéfice \"{benefice.Libelle}\" arrive à échéance d'évaluation aujourd'hui sur le projet \"{benefice.Projet.Titre}\".",
                    DomainEntityTypes.BeneficeProjet,
                    benefice.Id);
            }
        }

        private static async Task SuggererAvenantsAsync(ApplicationDbContext db, INotificationService notifications, CancellationToken ct)
        {
            const decimal seuilEcartBudgetaire = 0.15m; // 15%
            const int seuilEcartJours = 15;

            var projetsActifs = await db.Projets
                .Include(p => p.FicheProjet)
                .Where(p => p.StatutProjet == StatutProjet.EnCours)
                .ToListAsync(ct);

            foreach (var projet in projetsActifs)
            {
                var motifs = new List<string>();

                var budgetPrevu = projet.FicheProjet?.BudgetPrevisionnel;
                var budgetConsomme = projet.FicheProjet?.BudgetConsomme;
                if (budgetPrevu is decimal prevu && prevu > 0 && budgetConsomme is decimal consomme)
                {
                    var ecart = Math.Abs(consomme - prevu) / prevu;
                    if (ecart > seuilEcartBudgetaire)
                    {
                        motifs.Add($"écart budgétaire de {ecart:P0}");
                    }
                }

                if (projet.EcartJoursDelai is int ecartJours && ecartJours > seuilEcartJours)
                {
                    motifs.Add($"retard de {ecartJours} jour(s) par rapport à la baseline");
                }

                if (motifs.Count == 0)
                {
                    continue;
                }

                var aUnAvenantEnCours = await db.AvenantsProjets.AnyAsync(a =>
                    a.ProjetId == projet.Id && !a.EstSupprime &&
                    (a.Statut == StatutAvenant.EnAttenteValidationDM || a.Statut == StatutAvenant.EnAttenteValidationDSI), ct);

                if (aUnAvenantEnCours)
                {
                    continue;
                }

                var destinataire = projet.ChefProjetId ?? projet.SponsorId;
                await notifications.NotifierUtilisateurAsync(
                    destinataire,
                    TypeNotification.AvenantSuggere,
                    $"Avenant suggéré - {projet.CodeProjet}",
                    $"Le projet \"{projet.Titre}\" présente un {string.Join(" et un ", motifs)}. Un avenant pourrait être nécessaire pour officialiser le changement.",
                    DomainEntityTypes.AvenantSuggestion,
                    projet.Id);
            }
        }
    }
}
