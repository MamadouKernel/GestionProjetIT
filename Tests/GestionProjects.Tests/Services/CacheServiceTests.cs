using FluentAssertions;
using GestionProjects.Application.Common.Interfaces;
using GestionProjects.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GestionProjects.Tests.Services
{
    public class CacheServiceTests
    {
        private readonly IMemoryCache _cache;
        private readonly Mock<ILogger<CacheService>> _loggerMock;
        private readonly ICacheService _service;

        public CacheServiceTests()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
            _loggerMock = new Mock<ILogger<CacheService>>();
            _service = new CacheService(_cache, _loggerMock.Object);
        }

        [Fact]
        public async Task GetOrSetAsync_QuandCacheVide_ExecuteFactoryEtMetEnCache()
        {
            // Arrange
            var key = "test_key";
            var factoryCallCount = 0;

            // Act
            var result1 = await _service.GetOrSetAsync(key, async () =>
            {
                factoryCallCount++;
                await Task.Delay(10);
                return "test_value";
            });

            var result2 = await _service.GetOrSetAsync(key, async () =>
            {
                factoryCallCount++;
                await Task.Delay(10);
                return "test_value";
            });

            // Assert
            result1.Should().Be("test_value");
            result2.Should().Be("test_value");
            factoryCallCount.Should().Be(1); // Factory appelée une seule fois
        }

        [Fact]
        public async Task GetOrSetAsync_QuandCacheExpire_ReexecuteFactory()
        {
            // Arrange
            var key = "test_key_expire";
            var factoryCallCount = 0;

            // Act - Premier appel avec expiration courte
            var result1 = await _service.GetOrSetAsync(key, async () =>
            {
                factoryCallCount++;
                await Task.Delay(10);
                return "test_value";
            }, TimeSpan.FromMilliseconds(100));

            await Task.Delay(150); // Attendre l'expiration

            var result2 = await _service.GetOrSetAsync(key, async () =>
            {
                factoryCallCount++;
                await Task.Delay(10);
                return "new_value";
            }, TimeSpan.FromMilliseconds(100));

            // Assert
            factoryCallCount.Should().Be(2); // Factory appelée deux fois
            result2.Should().Be("new_value");
        }

        [Fact]
        public void Remove_QuandCleExiste_SupprimeDuCache()
        {
            // Arrange
            var key = "test_remove";
            _cache.Set(key, "test_value");

            // Act
            _service.Remove(key);

            // Assert
            _cache.TryGetValue(key, out _).Should().BeFalse();
        }
    }
}

