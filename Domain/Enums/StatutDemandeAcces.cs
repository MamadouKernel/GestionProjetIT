namespace GestionProjects.Domain.Enums
{
    /// <summary>
    /// Cycle de vie d'une demande d'accès :
    /// EnAttente (= en attente validation DM) → ApprouveeParDm (en attente création par
    /// AdminIT/DSI/RSIT) → Approuvee (compte créé, lien d'activation envoyé).
    /// RejeteeParDm si le DM refuse (terminal, demandeur informé).
    /// Rejetee si l'AdminIT/DSI/RSIT refuse après validation DM (rare, terminal).
    /// </summary>
    public enum StatutDemandeAcces
    {
        EnAttente = 1,        // = en attente DM (statut historique, gardé pour compat données)
        Approuvee = 2,        // = compte créé, accès actif (statut historique)
        Rejetee = 3,          // refus AdminIT/DSI/RSIT (terminal)
        RejeteeParDm = 4,     // refus DM (terminal)
        ApprouveeParDm = 5    // validée DM, en attente création compte
    }
}
