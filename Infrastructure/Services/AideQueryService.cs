using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Infrastructure.Services;

public sealed class AideQueryService : IAideQueryService
{
    private readonly ApplicationDbContext _db;

    public AideQueryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AideUserContext?> GetUserContextAsync(Guid userId)
    {
        var user = await _db.Utilisateurs
            .Include(u => u.UtilisateurRoles)
            .Include(u => u.Direction)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return null;
        }

        var roles = user.UtilisateurRoles
            .Where(ur => !ur.EstSupprime &&
                         (!ur.DateDebut.HasValue || ur.DateDebut.Value <= DateTime.Now) &&
                         (!ur.DateFin.HasValue || ur.DateFin.Value >= DateTime.Now))
            .Select(ur => ur.Role)
            .ToList();

        return new AideUserContext(
            roles,
            $"{user.Nom} {user.Prenoms}",
            user.Direction?.Libelle ?? "Non assigne");
    }
}
