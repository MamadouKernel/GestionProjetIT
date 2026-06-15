using FluentAssertions;
using Xunit;
using ProjetModel = GestionProjects.Domain.Models.Projet;

namespace GestionProjects.Tests.Unit.Projet;

/// <summary>
/// Tests de la dérive de délai par rapport à la baseline (référentiel figé).
/// </summary>
public class ProjetBaselineTests
{
    [Fact]
    public void EcartJoursDelai_EstNull_SansBaseline()
    {
        var p = new ProjetModel { DateFinPrevue = new DateTime(2026, 12, 31) };
        p.EcartJoursDelai.Should().BeNull();
    }

    [Fact]
    public void EcartJoursDelai_EstPositif_QuandRetardSurBaseline()
    {
        var p = new ProjetModel
        {
            DateFinPrevueBaseline = new DateTime(2026, 12, 31),
            DateFinPrevue = new DateTime(2027, 1, 30)
        };
        p.EcartJoursDelai.Should().Be(30);
    }

    [Fact]
    public void EcartJoursDelai_EstNegatif_QuandAvanceSurBaseline()
    {
        var p = new ProjetModel
        {
            DateFinPrevueBaseline = new DateTime(2026, 12, 31),
            DateFinPrevue = new DateTime(2026, 12, 21)
        };
        p.EcartJoursDelai.Should().Be(-10);
    }
}
