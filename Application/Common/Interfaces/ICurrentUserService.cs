namespace GestionProjects.Application.Common.Interfaces
{
    public interface ICurrentUserService
    {
        string Matricule { get; }
        IEnumerable<string> Roles { get; }
    }
}
