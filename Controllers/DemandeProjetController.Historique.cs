using GestionProjects.Application.Common.Extensions;
using GestionProjects.Application.ViewModels.DemandeProjet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionProjects.Controllers
{
    public partial class DemandeProjetController
    {
        // GET: Historique des actions du Directeur Métier sur les demandes
        [Authorize]
        public async Task<IActionResult> HistoriqueActionsDM(int page = 1, int pageSize = 20)
        {
            var userId = User.GetUserIdOrThrow();
            var actionsDM = new[] { "VALIDATION_DM", "REJET_DM", "CORRECTION_DM" };

            var logsQuery = _db.AuditLogs
                .Include(a => a.Utilisateur)
                .Where(a => actionsDM.Contains(a.TypeAction) && a.Entite == "DemandeProjet" && !a.EstSupprime);

            if (!await HasAdminScopeAsync())
                logsQuery = logsQuery.Where(a => a.UtilisateurId == userId);

            var total = await logsQuery.CountAsync();
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 50);
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);

            var logs = await logsQuery
                .OrderByDescending(a => a.DateAction)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var demandeIds = logs
                .Where(l => Guid.TryParse(l.EntiteId, out _))
                .Select(l => Guid.Parse(l.EntiteId))
                .Distinct()
                .ToList();

            var demandes = await _db.DemandesProjets
                .Include(d => d.Demandeur)
                .Include(d => d.Direction)
                .Where(d => demandeIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id);

            var vm = new HistoriqueActionsDMViewModel
            {
                Logs       = logs,
                Demandes   = demandes,
                Page       = page,
                TotalPages = totalPages,
                Total      = total
            };

            return View(vm);
        }
    }
}
