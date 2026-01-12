namespace GestionProjects.Application.Common.Interfaces
{
    /// <summary>
    /// Service de cache pour les données fréquemment consultées
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Obtient une valeur du cache ou l'exécute et la met en cache
        /// </summary>
        Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class;

        /// <summary>
        /// Supprime une clé du cache
        /// </summary>
        void Remove(string key);

        /// <summary>
        /// Supprime toutes les clés correspondant au préfixe
        /// </summary>
        void RemoveByPrefix(string prefix);
    }
}

