using GestionProjects.Domain.Enums;

namespace GestionProjects.Application.Common.Security
{
    public static class PermissionCacheKeys
    {
        public static string GetRoleVersionKey(RoleUtilisateur role) => $"Permissions_Version_{role}";

        public static string GetPermissionKey(RoleUtilisateur role, string controleur, string action, long version) =>
            $"Permissions_{role}_{controleur}_{action}_v{version}";

        public static string GetPermissionListKey(RoleUtilisateur role, long version) =>
            $"Permissions_List_{role}_v{version}";
    }
}
