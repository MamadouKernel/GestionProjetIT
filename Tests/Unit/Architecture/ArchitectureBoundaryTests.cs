using FluentAssertions;
using GestionProjects.Application.Common.Interfaces;
using Xunit;

namespace GestionProjects.Tests.Unit.Architecture;

public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void Domain_NeDependPasDesCouchesExternes()
    {
        var references = ReferenceNames(typeof(global::GestionProjects.Domain.Models.Projet).Assembly);

        references.Should().NotContain("GestionProjects.Application");
        references.Should().NotContain("GestionProjects.Infrastructure");
        references.Should().NotContain("GestionProjects");
        references.Should().NotContain(reference => reference.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        references.Should().NotContain(reference => reference.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_NeDependPasDeWebInfrastructureAspNetOuEfCore()
    {
        var references = ReferenceNames(typeof(IProjetQueryService).Assembly);

        references.Should().NotContain("GestionProjects.Infrastructure");
        references.Should().NotContain("GestionProjects");
        references.Should().NotContain(reference => reference.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        references.Should().NotContain(reference => reference.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    private static IReadOnlyCollection<string> ReferenceNames(System.Reflection.Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();
    }
}
