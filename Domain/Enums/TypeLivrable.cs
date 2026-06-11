namespace GestionProjects.Domain.Enums
{
    public enum TypeLivrable
    {
        CahierCharges = 1,
        CahierAnalyseTechnique = 2,
        CharteProjet = 3,
        NoteCadrage = 4,
        CharteProjetSignee = 9,

        Wbs = 10,
        PlanningDetaille = 11,
        MatriceRaci = 12,
        SchemaCommunication = 13,
        BudgetPrevisionnel = 14,
        PvKickOff = 15,

        CahierTests = 20,
        FeuilleAnomalies = 21,
        PvRecette = 22,
        RapportHypercare = 23,

        DossierMep = 30,
        PvMep = 31,

        RapportCloture = 40,
        PvCloture = 41,
        DossierExploitation = 42,

        CompteRenduReunion = 50,

        // Documents financiers / achats
        Devis = 60,
        BonCommande = 61,
        Facture = 62,
        MemoInterne = 63,

        Autre = 99
    }
}
