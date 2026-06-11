using FluentAssertions;
using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Helpers;
using Xunit;

namespace GestionProjects.Tests.Unit.DemandeProjet;

public class PrioriteDemandeHelperTests
{
    [Theory]
    [InlineData(UrgenceProjet.Haute, CriticiteProjet.Critique, "P1", 7)]
    [InlineData(UrgenceProjet.Haute, CriticiteProjet.Elevee, "P2", 6)]
    [InlineData(UrgenceProjet.Haute, CriticiteProjet.Moyenne, "P3", 5)]
    [InlineData(UrgenceProjet.Moyenne, CriticiteProjet.Moyenne, "P4", 4)]
    [InlineData(UrgenceProjet.Basse, CriticiteProjet.Moyenne, "P5", 3)]
    [InlineData(UrgenceProjet.Basse, CriticiteProjet.Faible, "P6", 2)]
    public void GetPrioriteCode_ShouldReturnExpectedPriority(
        UrgenceProjet urgence,
        CriticiteProjet criticite,
        string expectedCode,
        int expectedScore)
    {
        var code = PrioriteDemandeHelper.GetPrioriteCode(urgence, criticite);
        var score = PrioriteDemandeHelper.CalculateScore(urgence, criticite);

        code.Should().Be(expectedCode);
        score.Should().Be(expectedScore);
    }

    [Theory]
    [InlineData(UrgenceProjet.Haute, CriticiteProjet.Critique, "Critique", "danger")]
    [InlineData(UrgenceProjet.Haute, CriticiteProjet.Elevee, "Tres elevee", "warning")]
    [InlineData(UrgenceProjet.Haute, CriticiteProjet.Moyenne, "Elevee", "info")]
    [InlineData(UrgenceProjet.Basse, CriticiteProjet.Faible, "Planifiee", "secondary")]
    public void GetPrioriteMetadata_ShouldReturnExpectedLabelAndBadge(
        UrgenceProjet urgence,
        CriticiteProjet criticite,
        string expectedLabel,
        string expectedBadgeClass)
    {
        var label = PrioriteDemandeHelper.GetPrioriteLibelle(urgence, criticite);
        var badgeClass = PrioriteDemandeHelper.GetPrioriteBadgeClass(urgence, criticite);

        label.Should().Be(expectedLabel);
        badgeClass.Should().Be(expectedBadgeClass);
    }
}
