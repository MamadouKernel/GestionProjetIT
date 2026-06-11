using FluentAssertions;
using GestionProjects.Application.Common.Helpers;
using GestionProjects.Domain.Enums;
using Xunit;

namespace GestionProjects.Tests.Services
{
    /// <summary>
    /// Tests des règles métier d'import Excel via ImportUserValidator.
    /// </summary>
    public class ImportUsersTests
    {
        // ---------------------------------------------------------------
        // ValidateRow
        // ---------------------------------------------------------------

        [Fact]
        public void ValidateRow_EmailInvalide_RetourneErreur()
        {
            // Act
            var erreur = ImportUserValidator.ValidateRow("MAT001", "Dupont", "Jean", "email-invalide");

            // Assert
            erreur.Should().NotBeNull();
            erreur!.ToLowerInvariant().Should().Contain("email");
        }

        [Fact]
        public void ValidateRow_MatriculeManquant_RetourneErreur()
        {
            // Act
            var erreur = ImportUserValidator.ValidateRow("", "Dupont", "Jean", "jean@test.com");

            // Assert
            erreur.Should().NotBeNull();
            erreur!.ToLowerInvariant().Should().Contain("matricule");
        }

        [Fact]
        public void ValidateRow_LigneValide_RetourneNull()
        {
            // Act
            var erreur = ImportUserValidator.ValidateRow("MAT001", "Dupont", "Jean", "jean@test.com");

            // Assert
            erreur.Should().BeNull();
        }

        // ---------------------------------------------------------------
        // ParseRoles
        // ---------------------------------------------------------------

        [Fact]
        public void ParseRoles_RoleInvalide_EstIgnoreEtDemandeurAssigneParDefaut()
        {
            // Act — "SuperAdmin" n'est pas dans l'enum
            var (roles, rolesInvalides) = ImportUserValidator.ParseRoles("SuperAdmin");

            // Assert
            roles.Should().ContainSingle().Which.Should().Be(RoleUtilisateur.Demandeur);
            rolesInvalides.Should().Contain("SuperAdmin");
        }

        [Fact]
        public void ParseRoles_RolesMixtes_RolesInvalidesIgnoresRolesValidesConserves()
        {
            // Act — "Demandeur" est valide, "RoleFantome" ne l'est pas
            var (roles, rolesInvalides) = ImportUserValidator.ParseRoles("Demandeur,RoleFantome");

            // Assert
            roles.Should().Contain(RoleUtilisateur.Demandeur);
            rolesInvalides.Should().Contain("RoleFantome");
        }

        // ---------------------------------------------------------------
        // ResoudreSatutDoublon
        // ---------------------------------------------------------------

        [Fact]
        public void ResoudreSatutDoublon_IgnorerDoublonsTrue_RetourneIgnore()
        {
            // Act
            var statut = ImportUserValidator.ResoudreSatutDoublon(ignorerDoublons: true);

            // Assert
            statut.Should().Be("Ignoré");
        }

        [Fact]
        public void ResoudreSatutDoublon_IgnorerDoublonsFalse_RetourneErreur()
        {
            // Act
            var statut = ImportUserValidator.ResoudreSatutDoublon(ignorerDoublons: false);

            // Assert
            statut.Should().Be("Erreur");
        }
    }
}
