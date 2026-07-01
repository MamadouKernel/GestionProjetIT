using GestionProjects.Domain.Models;

namespace GestionProjects.Application.ViewModels.Projet;

public class PortefeuilleViewModel
{
    public IEnumerable<Domain.Models.Projet> Projets { get; set; } = Enumerable.Empty<Domain.Models.Projet>();
    public PortefeuilleProjet Portefeuille { get; set; } = new();
    public IEnumerable<GestionProjects.Domain.Models.DemandeProjet> DemandesEnCours { get; set; } = Enumerable.Empty<GestionProjects.Domain.Models.DemandeProjet>();

    /// <summary>Avantages attendus renseignés au niveau de chaque projet du portefeuille (issus de la demande d'origine).</summary>
    public IReadOnlyList<AvantageProjetPortefeuille> AvantagesParProjet { get; set; } = Array.Empty<AvantageProjetPortefeuille>();

    /// <summary>Risques et mitigations saisis pour chaque projet du portefeuille.</summary>
    public IReadOnlyList<RisquesProjetPortefeuille> RisquesParProjet { get; set; } = Array.Empty<RisquesProjetPortefeuille>();
}

public class AvantageProjetPortefeuille
{
    public string ProjetTitre { get; set; } = string.Empty;
    public string AvantagesAttendus { get; set; } = string.Empty;
}

public class RisquesProjetPortefeuille
{
    public string ProjetTitre { get; set; } = string.Empty;
    public IReadOnlyList<RisqueProjet> Risques { get; set; } = Array.Empty<RisqueProjet>();
}
