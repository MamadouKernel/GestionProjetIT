using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.Common.Interfaces;

public interface IAideQueryService
{
    Task<AideUserContext?> GetUserContextAsync(Guid userId);
}

public sealed record AideUserContext(
    IReadOnlyList<RoleUtilisateur> Roles,
    string UserName,
    string UserDirection);
