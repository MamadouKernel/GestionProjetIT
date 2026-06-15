using FluentAssertions;
using GestionProjects.Domain.Enums;
using Xunit;
using ProjetModel = GestionProjects.Domain.Models.Projet;

namespace GestionProjects.Tests.Unit.Projet;

/// <summary>
/// Tests de la cohérence santé : le RAG calculé est la référence objective,
/// l'État déclaré peut diverger et la divergence est détectée.
/// </summary>
public class ProjetSanteTests
{
    [Theory]
    [InlineData(IndicateurRAG.Vert, EtatProjet.Vert)]
    [InlineData(IndicateurRAG.Amber, EtatProjet.Orange)]
    [InlineData(IndicateurRAG.Rouge, EtatProjet.Rouge)]
    public void EtatRAGEquivalent_MappeLeRAGSurLEtat(IndicateurRAG rag, EtatProjet attendu)
    {
        var p = new ProjetModel { IndicateurRAG = rag };
        p.EtatRAGEquivalent.Should().Be(attendu);
    }

    [Fact]
    public void EtatDivergeDuRAG_FauxQuandAlignes()
    {
        var p = new ProjetModel { IndicateurRAG = IndicateurRAG.Rouge, EtatProjet = EtatProjet.Rouge };
        p.EtatDivergeDuRAG.Should().BeFalse();
    }

    [Fact]
    public void EtatDivergeDuRAG_VraiQuandPiloteTropOptimiste()
    {
        var p = new ProjetModel { IndicateurRAG = IndicateurRAG.Rouge, EtatProjet = EtatProjet.Vert };
        p.EtatDivergeDuRAG.Should().BeTrue();
    }
}
