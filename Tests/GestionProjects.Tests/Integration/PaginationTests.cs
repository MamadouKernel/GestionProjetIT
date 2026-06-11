using FluentAssertions;
using GestionProjects.Application.Common.Extensions;
using GestionProjects.Domain.Models;
using GestionProjects.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionProjects.Tests.Integration
{
    /// <summary>
    /// Tests de pagination sur <see cref="QueryableExtensions.ToPagedResultAsync{T}"/>.
    /// Un DbContext en mémoire est utilisé pour fournir un vrai IQueryable EF.
    /// </summary>
    public class PaginationTests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        public PaginationTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>Insère <paramref name="count"/> directions avec des codes uniques.</summary>
        private async Task SeedDirectionsAsync(int count, string prefix = "D")
        {
            for (int i = 1; i <= count; i++)
            {
                _context.Directions.Add(new Direction
                {
                    Id = Guid.NewGuid(),
                    Code = $"{prefix}{i:D3}",
                    Libelle = $"Direction {i}",
                    EstActive = true,
                    EstSupprime = false,
                    DateCreation = DateTime.Now,
                    CreePar = "SYSTEM"
                });
            }
            await _context.SaveChangesAsync();
        }

        // -----------------------------------------------------------------------
        // 1. 20 éléments, pageSize = 5 → 4 pages
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Pagination_20Elements_PageSize5_Retourne4Pages()
        {
            // Arrange
            await SeedDirectionsAsync(20);

            // Act
            var result = await _context.Directions
                .Where(d => !d.EstSupprime)
                .ToPagedResultAsync(pageNumber: 1, pageSize: 5);

            // Assert
            result.TotalCount.Should().Be(20);
            result.TotalPages.Should().Be(4);
            result.PageSize.Should().Be(5);
            result.Items.Should().HaveCount(5);
            result.HasPreviousPage.Should().BeFalse();
            result.HasNextPage.Should().BeTrue();
        }

        // -----------------------------------------------------------------------
        // 2. 3 éléments, pageSize = 5 → 1 page, pas de navigation
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Pagination_3Elements_PageSize5_Retourne1Page_SansPagination()
        {
            // Arrange
            await SeedDirectionsAsync(3, "E");

            // Act
            var result = await _context.Directions
                .Where(d => !d.EstSupprime)
                .ToPagedResultAsync(pageNumber: 1, pageSize: 5);

            // Assert
            result.TotalCount.Should().Be(3);
            result.TotalPages.Should().Be(1);
            result.Items.Should().HaveCount(3);
            result.HasPreviousPage.Should().BeFalse();
            result.HasNextPage.Should().BeFalse();
        }

        // -----------------------------------------------------------------------
        // 3. Page 2 retourne les bons éléments (éléments 6-10 sur 20)
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Pagination_Page2_RetourneElementsCorrects()
        {
            // Arrange
            await SeedDirectionsAsync(20, "F");

            var tousLesCodes = await _context.Directions
                .Where(d => !d.EstSupprime)
                .OrderBy(d => d.Code)
                .Select(d => d.Code)
                .ToListAsync();

            var codesAttendusPage2 = tousLesCodes.Skip(5).Take(5).ToList();

            // Act
            var result = await _context.Directions
                .Where(d => !d.EstSupprime)
                .OrderBy(d => d.Code)
                .ToPagedResultAsync(pageNumber: 2, pageSize: 5);

            // Assert
            result.PageNumber.Should().Be(2);
            result.Items.Should().HaveCount(5);
            result.HasPreviousPage.Should().BeTrue();
            result.HasNextPage.Should().BeTrue();
            result.Items.Select(d => d.Code).Should().Equal(codesAttendusPage2);
        }

        // -----------------------------------------------------------------------
        // 4. Page hors limite → ramenée à 1 (pageNumber < 1 normalisé par Math.Max)
        // -----------------------------------------------------------------------
        [Fact]
        public async Task Pagination_PageHorsLimite_RameneA1()
        {
            // Arrange
            await SeedDirectionsAsync(10, "G");

            // Act — pageNumber = 0 ou négatif doit être normalisé à 1
            var result = await _context.Directions
                .Where(d => !d.EstSupprime)
                .ToPagedResultAsync(pageNumber: 0, pageSize: 5);

            // Assert
            result.PageNumber.Should().Be(1);
            result.Items.Should().HaveCount(5);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
