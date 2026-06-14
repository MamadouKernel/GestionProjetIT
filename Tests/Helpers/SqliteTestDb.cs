using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Tests.Helpers;

/// <summary>
/// Contexte de test adossé à un SQLite in-memory RELATIONNEL (et non au provider
/// InMemory). Permet de valider ce que l'InMemory ne sait pas faire : contraintes
/// d'unicité, index filtrés (partiels), filtres de requête globaux traduits en SQL.
///
/// La connexion SQLite ":memory:" ne vit que tant qu'elle reste ouverte : on la
/// garde ouverte pour la durée du contexte et on dispose les deux ensemble.
/// </summary>
public sealed class SqliteTestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public ApplicationDbContext Context { get; }

    public SqliteTestDb(ICurrentUserService? currentUserService = null)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new ApplicationDbContext(options, currentUserService);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
