namespace GestionProjects.Domain.Enums
{
    /// <summary>
    /// Indicateur RAG (Red/Amber/Green) pour le portefeuille de projets
    /// Utilisé pour la gouvernance et le reporting DSI/DG
    /// </summary>
    public enum IndicateurRAG
    {
        /// <summary>
        /// Vert : Projet en bonne santé, pas d'alerte
        /// </summary>
        Vert = 1,
        
        /// <summary>
        /// Amber/Orange : Projet nécessite une attention, vigilance requise
        /// </summary>
        Amber = 2,
        
        /// <summary>
        /// Rouge : Projet en difficulté, action corrective requise
        /// </summary>
        Rouge = 3
    }
}

