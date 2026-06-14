using FluentAssertions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Controllers;
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

    [Fact]
    public void ControleursMigres_NInjectentPasDirectementLeDbContext()
    {
        var migratedControllers = new[]
        {
            typeof(AideController),
            typeof(AutorisationsController),
            typeof(DemandesAccesController),
            typeof(NotificationController)
        };

        foreach (var controller in migratedControllers)
        {
            var constructorParameterNames = controller
                .GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType.FullName)
                .ToArray();

            constructorParameterNames.Should().NotContain(
                "GestionProjects.Infrastructure.Persistence.ApplicationDbContext",
                $"{controller.Name} doit passer par des services Application/Infrastructure, pas par EF directement");
        }
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
