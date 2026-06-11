using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Tests.Helpers;

public static class TestDbContextFactory
{
    public static ApplicationDbContext CreateInMemoryContext(string databaseName = "TestDb")
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .Options;

        var context = new ApplicationDbContext(options);
        return context;
    }

    public static async Task<ApplicationDbContext> CreateContextWithSeedDataAsync(string databaseName = "TestDb")
    {
        var context = CreateInMemoryContext(databaseName);
        await SeedTestDataAsync(context);
        return context;
    }

    private static async Task SeedTestDataAsync(ApplicationDbContext context)
    {
        // Créer des directions
        var directionDSI = new Direction
        {
            Id = Guid.NewGuid(),
            Code = "DSI",
            Libelle = "Direction des Systèmes d'Information",
            EstActive = true,
            DateCreation = DateTime.Now,
            CreePar = "SYSTEM"
        };

        var directionFinance = new Direction
        {
            Id = Guid.NewGuid(),
            Code = "FIN",
            Libelle = "Direction Financière",
            EstActive = true,
            DateCreation = DateTime.Now,
            CreePar = "SYSTEM"
        };

        context.Directions.AddRange(directionDSI, directionFinance);

        // Créer des utilisateurs
        var adminIT = new Utilisateur
        {
            Id = Guid.NewGuid(),
            Matricule = "admin",
            MotDePasse = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Nom = "Admin",
            Prenoms = "IT",
            Email = "admin@cit.ci",
            DateCreation = DateTime.Now,
            CreePar = "SYSTEM"
        };

        var demandeur = new Utilisateur
        {
            Id = Guid.NewGuid(),
            Matricule = "DEM001",
            MotDePasse = BCrypt.Net.BCrypt.HashPassword("Demandeur@123"),
            Nom = "Kouassi",
            Prenoms = "Jean",
            Email = "jean.kouassi@cit.ci",
            DirectionId = directionFinance.Id,
            DateCreation = DateTime.Now,
            CreePar = "SYSTEM"
        };

        var directeurMetier = new Utilisateur
        {
            Id = Guid.NewGuid(),
            Matricule = "DIR001",
            MotDePasse = BCrypt.Net.BCrypt.HashPassword("Directeur@123"),
            Nom = "Yao",
            Prenoms = "Marie",
            Email = "marie.yao@cit.ci",
            DirectionId = directionFinance.Id,
            DateCreation = DateTime.Now,
            CreePar = "SYSTEM"
        };

        var dsi = new Utilisateur
        {
            Id = Guid.NewGuid(),
            Matricule = "DSI001",
            MotDePasse = BCrypt.Net.BCrypt.HashPassword("DSI@123"),
            Nom = "Koffi",
            Prenoms = "Paul",
            Email = "paul.koffi@cit.ci",
            DirectionId = directionDSI.Id,
            DateCreation = DateTime.Now,
            CreePar = "SYSTEM"
        };

        var chefProjet = new Utilisateur
        {
            Id = Guid.NewGuid(),
            Matricule = "CP001",
            MotDePasse = BCrypt.Net.BCrypt.HashPassword("ChefProjet@123"),
            Nom = "Diallo",
            Prenoms = "Fatou",
            Email = "fatou.diallo@cit.ci",
            DirectionId = directionDSI.Id,
            DateCreation = DateTime.Now,
            CreePar = "SYSTEM"
        };

        var responsableSolutionsIT = new Utilisateur
        {
            Id = Guid.NewGuid(),
            Matricule = "RSI001",
            MotDePasse = BCrypt.Net.BCrypt.HashPassword("RespSol@123"),
            Nom = "Bamba",
            Prenoms = "Seydou",
            Email = "seydou.bamba@cit.ci",
            DirectionId = directionDSI.Id,
            DateCreation = DateTime.Now,
            CreePar = "SYSTEM"
        };

        context.Utilisateurs.AddRange(adminIT, demandeur, directeurMetier, dsi, chefProjet, responsableSolutionsIT);

        // Ajouter les rôles
        context.UtilisateurRoles.AddRange(
            new UtilisateurRole
            {
                Id = Guid.NewGuid(),
                UtilisateurId = adminIT.Id,
                Role = RoleUtilisateur.AdminIT,
                DateDebut = DateTime.Now,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            },
            new UtilisateurRole
            {
                Id = Guid.NewGuid(),
                UtilisateurId = demandeur.Id,
                Role = RoleUtilisateur.Demandeur,
                DateDebut = DateTime.Now,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            },
            new UtilisateurRole
            {
                Id = Guid.NewGuid(),
                UtilisateurId = directeurMetier.Id,
                Role = RoleUtilisateur.DirecteurMetier,
                DateDebut = DateTime.Now,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            },
            new UtilisateurRole
            {
                Id = Guid.NewGuid(),
                UtilisateurId = dsi.Id,
                Role = RoleUtilisateur.DSI,
                DateDebut = DateTime.Now,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            },
            new UtilisateurRole
            {
                Id = Guid.NewGuid(),
                UtilisateurId = chefProjet.Id,
                Role = RoleUtilisateur.ChefDeProjet,
                DateDebut = DateTime.Now,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            },
            new UtilisateurRole
            {
                Id = Guid.NewGuid(),
                UtilisateurId = responsableSolutionsIT.Id,
                Role = RoleUtilisateur.ResponsableSolutionsIT,
                DateDebut = DateTime.Now,
                DateCreation = DateTime.Now,
                CreePar = "SYSTEM"
            }
        );

        await context.SaveChangesAsync();
    }
}
