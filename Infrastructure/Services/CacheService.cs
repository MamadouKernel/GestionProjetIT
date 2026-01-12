using GestionProjects.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace GestionProjects.Infrastructure.Services
{
    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CacheService> _logger;

        public CacheService(IMemoryCache cache, ILogger<CacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            if (_cache.TryGetValue(key, out T? cachedValue))
            {
                _logger.LogDebug("Cache hit pour la clé: {Key}", key);
                return cachedValue;
            }

            _logger.LogDebug("Cache miss pour la clé: {Key}, exécution de la factory", key);
            var value = await factory();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(15),
                SlidingExpiration = TimeSpan.FromMinutes(5),
                Priority = CacheItemPriority.Normal
            };

            _cache.Set(key, value, cacheOptions);
            return value;
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
            _logger.LogDebug("Clé supprimée du cache: {Key}", key);
        }

        public void RemoveByPrefix(string prefix)
        {
            // Note: IMemoryCache ne supporte pas directement la suppression par préfixe
            // Cette implémentation nécessiterait un wrapper ou une liste de clés
            // Pour l'instant, on log juste l'intention
            _logger.LogWarning("RemoveByPrefix appelé pour {Prefix} - non implémenté avec IMemoryCache", prefix);
        }
    }
}

