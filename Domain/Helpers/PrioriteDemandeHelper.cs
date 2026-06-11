using GestionProjects.Domain.Enums;

namespace GestionProjects.Domain.Helpers
{
    public static class PrioriteDemandeHelper
    {
        public static int CalculateScore(UrgenceProjet urgence, CriticiteProjet criticite)
        {
            return (int)urgence + (int)criticite;
        }

        public static string GetPrioriteCode(UrgenceProjet urgence, CriticiteProjet criticite)
        {
            var score = CalculateScore(urgence, criticite);
            var priorityIndex = Math.Clamp(8 - score, 1, 6);
            return $"P{priorityIndex}";
        }

        public static string GetPrioriteLibelle(UrgenceProjet urgence, CriticiteProjet criticite)
        {
            return GetPrioriteCode(urgence, criticite) switch
            {
                "P1" => "Critique",
                "P2" => "Tres elevee",
                "P3" => "Elevee",
                "P4" => "Moderee",
                "P5" => "Faible",
                _ => "Planifiee"
            };
        }

        public static string GetPrioriteBadgeClass(UrgenceProjet urgence, CriticiteProjet criticite)
        {
            return GetPrioriteCode(urgence, criticite) switch
            {
                "P1" => "danger",
                "P2" => "warning",
                "P3" => "info",
                _ => "secondary"
            };
        }
    }
}
