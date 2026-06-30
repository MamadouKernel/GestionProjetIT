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
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var aujourdhui = DateTime.Today;

            if (aujourdhui.DayOfWeek == DayOfWeek.Thursday)
            {
                await RappellerChargesManquantesAsync(db, notifications, emailService, aujourdhui, cancellationToken);
            }

            await RappellerBeneficesAEvaluerAsync(db, notifications, emailService, aujourdhui, cancellationToken);

            if (aujourdhui.DayOfWeek == DayOfWeek.Monday)
            {
                await SuggererAvenantsAsync(db, notifications, emailService, cancellationToken);
            }
        }

        /// <summary>
        /// Envoie aussi un e-mail (en plus de la notification en application) au destinataire,
        /// si son adresse est connue. Réutilise IEmailService.EnvoyerAsync, qui gère déjà le
        /// flag SmtpSettings:Enabled et les erreurs SMTP en interne — pas de try/catch requis ici.
        /// </summary>
        private static async Task EnvoyerEmailRappelAsync(
            ApplicationDbContext db, IEmailService emailService, Guid destinataireId, string sujet, string corpsTexte)
        {
            var email = await db.Utilisateurs
                .Where(u => u.Id == destinataireId && !u.EstSupprime)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(email))
            {
                await emailService.EnvoyerAsync(email, sujet, $"<p>{corpsTexte}</p>");
            }
        }

        private static async Task RappellerChargesManquantesAsync(
            ApplicationDbContext db, INotificationService notifications, IEmailService emailService, DateTime aujourdhui, CancellationToken ct)
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
                        var sujet = $"Charge hebdomadaire non saisie - {projet.CodeProjet}";
                        var message = $"Vous n'avez pas encore saisi votre charge de la semaine du {lundiSemaineCourante:dd/MM/yyyy} sur le projet \"{projet.Titre}\".";

                        await notifications.NotifierUtilisateurAsync(
                            ressourceId, TypeNotification.ChargeHebdomadaireManquante, sujet, message,
                            DomainEntityTypes.ChargeProjet, projet.Id);
                        await EnvoyerEmailRappelAsync(db, emailService, ressourceId, sujet, message);
                    }
                }
            }
        }

        private static async Task RappellerBeneficesAEvaluerAsync(
            ApplicationDbContext db, INotificationService notifications, IEmailService emailService, DateTime aujourdhui, CancellationToken ct)
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
                var sujet = $"Bénéfice à évaluer - {benefice.Projet.CodeProjet}";
                var message = $"Le bénéfice \"{benefice.Libelle}\" arrive à échéance d'évaluation aujourd'hui sur le projet \"{benefice.Projet.Titre}\".";

                await notifications.NotifierUtilisateurAsync(
                    destinataire, TypeNotification.BeneficeAEvaluer, sujet, message,
                    DomainEntityTypes.BeneficeProjet, benefice.Id);
                await EnvoyerEmailRappelAsync(db, emailService, destinataire, sujet, message);
            }
        }

        private static async Task SuggererAvenantsAsync(
            ApplicationDbContext db, INotificationService notifications, IEmailService emailService, CancellationToken ct)
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
                var sujet = $"Avenant suggéré - {projet.CodeProjet}";
                var message = $"Le projet \"{projet.Titre}\" présente un {string.Join(" et un ", motifs)}. Un avenant pourrait être nécessaire pour officialiser le changement.";

                await notifications.NotifierUtilisateurAsync(
                    destinataire, TypeNotification.AvenantSuggere, sujet, message,
                    DomainEntityTypes.AvenantSuggestion, projet.Id);
                await EnvoyerEmailRappelAsync(db, emailService, destinataire, sujet, message);
            }
        }
    }
}
