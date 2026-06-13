using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Application.Common.Results;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public class UatProjetWorkflowService : IUatProjetWorkflowService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUatValidationService _uatValidation;

    public UatProjetWorkflowService(
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IUatValidationService uatValidation)
    {
        _db = db;
        _currentUserService = currentUserService;
        _uatValidation = uatValidation;
    }

    public async Task<WorkflowResult> AjouterCasTestAsync(
        Guid projetId,
        string titre,
        string? description,
        string? resultatAttendu,
        PrioriteAnomalie priorite,
        bool estObligatoire,
        Guid? campagneId)
    {
        var projet = await _db.Projets.FindAsync(projetId);
        if (projet == null)
            return WorkflowResult.NotFound();

        if (string.IsNullOrWhiteSpace(titre))
            return WorkflowResult.Error("Le titre du cas de test est obligatoire.");

        var reference = await _uatValidation.GenererReferenceCasTestAsync(projet);

        var casTest = new CasTestProjet
        {
            Id = Guid.NewGuid(),
            ProjetId = projetId,
            CampagneTestProjetId = campagneId,
            Reference = reference,
            Titre = titre,
            Description = description ?? string.Empty,
            ResultatAttendu = resultatAttendu ?? string.Empty,
            Priorite = priorite,
            EstObligatoire = estObligatoire,
            CreePar = _currentUserService.Matricule ?? "SYSTEM",
            DateCreation = DateTime.Now
        };

        _db.CasTestsProjets.Add(casTest);
        await _db.SaveChangesAsync();

        return WorkflowResult.Success($"Cas de test {reference} ajouté.");
    }

    public async Task<WorkflowResult> ExecuterCasTestAsync(
        Guid projetId,
        Guid casTestId,
        StatutExecutionTest statut,
        string? commentaire,
        Guid? campagneId,
        Guid executeParId)
    {
        var casTest = await _db.CasTestsProjets.FindAsync(casTestId);
        if (casTest == null || casTest.ProjetId != projetId)
            return WorkflowResult.NotFound();

        var execution = new ExecutionTestProjet
        {
            Id = Guid.NewGuid(),
            ProjetId = projetId,
            CasTestProjetId = casTestId,
            CampagneTestProjetId = campagneId ?? casTest.CampagneTestProjetId,
            Statut = statut,
            Commentaire = commentaire ?? string.Empty,
            DateExecution = DateTime.Now,
            ExecuteParId = executeParId,
            CreePar = _currentUserService.Matricule ?? "SYSTEM",
            DateCreation = DateTime.Now
        };

        _db.ExecutionsTestsProjets.Add(execution);
        await _db.SaveChangesAsync();

        return WorkflowResult.Success($"Résultat enregistré : {statut}.");
    }

    public async Task<WorkflowResult> AjouterCampagneTestAsync(
        Guid projetId,
        string nom,
        string? descriptionCampagne,
        Environnement environnement,
        DateTime dateLancement)
    {
        if (!await _db.Projets.AnyAsync(p => p.Id == projetId))
            return WorkflowResult.NotFound();

        if (string.IsNullOrWhiteSpace(nom))
            return WorkflowResult.Error("Le nom de la campagne est obligatoire.");

        var campagne = new CampagneTestProjet
        {
            Id = Guid.NewGuid(),
            ProjetId = projetId,
            Nom = nom,
            Description = descriptionCampagne ?? string.Empty,
            Environnement = environnement,
            Statut = StatutCampagneTest.Brouillon,
            DateLancement = dateLancement,
            CreePar = _currentUserService.Matricule ?? "SYSTEM",
            DateCreation = DateTime.Now
        };

        _db.CampagnesTestsProjets.Add(campagne);
        await _db.SaveChangesAsync();

        return WorkflowResult.Success($"Campagne \"{nom}\" créée.");
    }

    public async Task<WorkflowResult> SupprimerCasTestAsync(Guid projetId, Guid casTestId)
    {
        var casTest = await _db.CasTestsProjets.FindAsync(casTestId);
        if (casTest == null || casTest.ProjetId != projetId)
            return WorkflowResult.NotFound();

        casTest.EstSupprime = true;
        casTest.ModifiePar = _currentUserService.Matricule ?? "SYSTEM";
        casTest.DateModification = DateTime.Now;
        await _db.SaveChangesAsync();

        return WorkflowResult.Success("Cas de test supprimé.");
    }
}
