using FluentAssertions;
using GestionProjects.Application.Common.Helpers;
using Xunit;

namespace GestionProjects.Tests.Unit.Common;

public class ValidationHelperTests
{
    [Theory]
    [InlineData("AdminTemp123", true)]
    [InlineData("MotDePasse2026", true)]
    [InlineData("courtpwd9", false)]
    [InlineData("motdepasse1234", false)]
    [InlineData("MOTDEPASSELONG", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsStrongPassword_ShouldEnforceExpectedPolicy(string? password, bool expected)
    {
        ValidationHelper.IsStrongPassword(password).Should().Be(expected);
    }
}
