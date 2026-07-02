using GestionProjects.Application.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;

namespace GestionProjects.Controllers
{
    // Import Excel des utilisateurs — concern volumineux et distinct du CRUD,
    // isolé ici. Candidat à une extraction vers un IUserImportService dédié.
    public partial class AdminController
    {
        [HttpGet]
        public IActionResult ImportUsers()
        {
            return View(new ImportUsersViewModel());
        }

        [HttpGet]
        public IActionResult DownloadModeleImportUsers()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Utilisateurs");

                worksheet.Cells[1, 1].Value = "Matricule";
                worksheet.Cells[1, 2].Value = "Nom";
                worksheet.Cells[1, 3].Value = "Prénoms";
                worksheet.Cells[1, 4].Value = "Email";
                worksheet.Cells[1, 5].Value = "Code Direction";
                worksheet.Cells[1, 6].Value = "Libellé Direction";
                worksheet.Cells[1, 7].Value = "Rôles";
                worksheet.Cells[1, 8].Value = "Peut Créer Demande";

                using (var range = worksheet.Cells[1, 1, 1, 8])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(68, 129, 192));
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                }

                worksheet.Cells[2, 1].Value = "EMP001";
                worksheet.Cells[2, 2].Value = "DUPONT";
                worksheet.Cells[2, 3].Value = "Jean";
                worksheet.Cells[2, 4].Value = "jean.dupont@cit.ci";
                worksheet.Cells[2, 5].Value = "DSI";
                worksheet.Cells[2, 6].Value = "Direction Système d'Information";
                worksheet.Cells[2, 7].Value = "Demandeur";
                worksheet.Cells[2, 8].Value = "Oui";

                worksheet.Cells[3, 1].Value = "EMP002";
                worksheet.Cells[3, 2].Value = "MARTIN";
                worksheet.Cells[3, 3].Value = "Marie";
                worksheet.Cells[3, 4].Value = "marie.martin@cit.ci";
                worksheet.Cells[3, 5].Value = "DSI";
                worksheet.Cells[3, 6].Value = "Direction Système d'Information";
                worksheet.Cells[3, 7].Value = "ChefDeProjet,DSI";
                worksheet.Cells[3, 8].Value = "Oui";

                worksheet.Cells[4, 1].Value = "EMP003";
                worksheet.Cells[4, 2].Value = "BERNARD";
                worksheet.Cells[4, 3].Value = "Pierre";
                worksheet.Cells[4, 4].Value = "pierre.bernard@cit.ci";
                worksheet.Cells[4, 5].Value = "";
                worksheet.Cells[4, 6].Value = "";
                worksheet.Cells[4, 7].Value = "Demandeur";
                worksheet.Cells[4, 8].Value = "Non";

                worksheet.Column(1).Width = 15;
                worksheet.Column(2).Width = 20;
                worksheet.Column(3).Width = 20;
                worksheet.Column(4).Width = 30;
                worksheet.Column(5).Width = 15;
                worksheet.Column(6).Width = 35;
                worksheet.Column(7).Width = 25;
                worksheet.Column(8).Width = 18;

                var excelBytes = package.GetAsByteArray();
                var fileName = $"Modele_Import_Utilisateurs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportUsers(IFormFile fichierExcel, string motDePasseParDefaut, bool ignorerDoublons = false)
        {
            using var stream = fichierExcel?.OpenReadStream();
            var result = await _userImportService.ImportUsersAsync(
                stream,
                fichierExcel?.FileName,
                fichierExcel?.Length ?? 0,
                motDePasseParDefaut,
                ignorerDoublons);

            if (!string.IsNullOrWhiteSpace(result.SuccessMessage))
            {
                TempData["Success"] = result.SuccessMessage;
            }

            return View(result.ViewModel);
        }    }
}
