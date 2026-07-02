using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace GestionProjects.Infrastructure.Services
{
    public interface IExcelService
    {
        Task<byte[]> GeneratePortefeuilleProjetsExcelAsync(PortefeuilleProjet portefeuille, List<Projet> projets);
        Task<byte[]> GenerateRapportDSIDGExcelAsync(List<Projet> projets);
        Task<byte[]> GeneratePlanningDetailleExcelAsync(Projet projet, IReadOnlyList<TachePlanningProjet> tasks);
        Task<byte[]> GenerateWbsExcelAsync(Projet projet, IReadOnlyList<TachePlanningProjet> tasks);
        Task<byte[]> GenerateMatriceRaciExcelAsync(Projet projet, IReadOnlyList<LigneRaciProjet> lines);
        Task<byte[]> GenerateSchemaCommunicationExcelAsync(Projet projet, IReadOnlyList<LigneCommunicationProjet> lines);
        Task<byte[]> GenerateBudgetPrevisionnelExcelAsync(Projet projet, IReadOnlyList<LigneBudgetPlanificationProjet> lines, FicheProjet? ficheProjet);
        Task<byte[]> GeneratePvKickOffExcelAsync(Projet projet, PvKickOffProjet kickOff);
    }

    public class ExcelService : IExcelService
    {
        private static readonly Color PrimaryBlue = Color.FromArgb(52, 129, 192);
        private static readonly Color SuccessGreen = Color.FromArgb(34, 197, 94);
        private static readonly Color WarningOrange = Color.FromArgb(249, 115, 22);
        private static readonly Color LightGray = Color.FromArgb(240, 240, 240);

        public ExcelService()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public async Task<byte[]> GeneratePortefeuilleProjetsExcelAsync(PortefeuilleProjet portefeuille, List<Projet> projets)
        {
            return await Task.Run(() =>
            {
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Portefeuille");

                var currentRow = ApplyBrandedHeader(
                    worksheet,
                    "Portefeuille projets",
                    portefeuille.Nom ?? "Portefeuille de Projet DSI",
                    9);

                currentRow = AddMergedSection(worksheet, currentRow, 1, 3, "OBJECTIF STRATEGIQUE GLOBAL", PrimaryBlue);
                worksheet.Cells[currentRow, 1].Value = portefeuille.ObjectifStrategiqueGlobal ?? "";
                worksheet.Cells[currentRow, 1, currentRow, 3].Merge = true;
                worksheet.Cells[currentRow, 1].Style.WrapText = true;
                currentRow += 2;

                currentRow = AddMergedSection(worksheet, currentRow, 1, 3, "AVANTAGES ATTENDUS DU PORTEFEUILLE", SuccessGreen);
                if (!string.IsNullOrWhiteSpace(portefeuille.AvantagesAttendus))
                {
                    foreach (var avantage in portefeuille.AvantagesAttendus.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var ligne = avantage.Trim().TrimStart('•').Trim();
                        worksheet.Cells[currentRow, 1].Value = $"• {ligne}";
                        worksheet.Cells[currentRow, 1, currentRow, 3].Merge = true;
                        currentRow++;
                    }
                }
                else
                {
                    worksheet.Cells[currentRow, 1].Value = "Aucun avantage renseigné.";
                    worksheet.Cells[currentRow, 1, currentRow, 3].Merge = true;
                    currentRow++;
                }
                currentRow++;

                currentRow = AddMergedSection(worksheet, currentRow, 1, 3, "RISQUES ET MITIGATIONS GLOBAUX", WarningOrange);
                if (!string.IsNullOrWhiteSpace(portefeuille.RisquesEtMitigations))
                {
                    foreach (var risque in portefeuille.RisquesEtMitigations.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var ligne = risque.Trim();
                        if (string.IsNullOrWhiteSpace(ligne))
                            continue;

                        var parts = ligne.Split(':', 2);
                        worksheet.Cells[currentRow, 1].Value = parts[0].Trim();
                        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                        worksheet.Cells[currentRow, 2].Value = parts.Length > 1 ? $"Mitigation: {parts[1].Trim()}" : "";
                        worksheet.Cells[currentRow, 2, currentRow, 3].Merge = true;
                        worksheet.Cells[currentRow, 2].Style.WrapText = true;
                        currentRow++;
                    }
                }
                else
                {
                    worksheet.Cells[currentRow, 1].Value = "Aucun risque global renseigné.";
                    worksheet.Cells[currentRow, 1, currentRow, 3].Merge = true;
                    currentRow++;
                }
                currentRow += 2;

                worksheet.Cells[currentRow, 1].Value = "PROJETS INCLUS DANS LE PORTEFEUILLE";
                worksheet.Cells[currentRow, 1].Style.Font.Size = 14;
                worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                worksheet.Cells[currentRow, 1, currentRow, 9].Merge = true;
                currentRow++;

                var headers = new[]
                {
                    "#", "Code Projet", "Titre", "Objectif", "Sponsor", "Chef Projet", "Statut", "Phase", "Avancement %"
                };

                WriteHeaderRow(worksheet, currentRow, headers, PrimaryBlue);
                currentRow++;

                var index = 1;
                foreach (var projet in projets)
                {
                    worksheet.Cells[currentRow, 1].Value = index++;
                    worksheet.Cells[currentRow, 2].Value = projet.CodeProjet;
                    worksheet.Cells[currentRow, 3].Value = projet.Titre;
                    worksheet.Cells[currentRow, 4].Value = projet.Objectif ?? "";
                    worksheet.Cells[currentRow, 4].Style.WrapText = true;
                    worksheet.Cells[currentRow, 5].Value = $"{projet.Sponsor?.Nom} {projet.Sponsor?.Prenoms}".Trim();
                    worksheet.Cells[currentRow, 6].Value = projet.ChefProjet != null
                        ? $"{projet.ChefProjet.Nom} {projet.ChefProjet.Prenoms}".Trim()
                        : "Non assigné";
                    worksheet.Cells[currentRow, 7].Value = projet.StatutProjet.ToString();
                    worksheet.Cells[currentRow, 8].Value = projet.PhaseActuelle.ToString();
                    worksheet.Cells[currentRow, 9].Value = projet.PourcentageAvancementAffiche / 100d;
                    worksheet.Cells[currentRow, 9].Style.Numberformat.Format = "0%";
                    ApplyTableRowBorder(worksheet, currentRow, headers.Length);
                    currentRow++;
                }

                AutoFitWorksheet(worksheet, 9);
                worksheet.Column(4).Width = 40;
                worksheet.Column(5).Width = 24;
                worksheet.Column(6).Width = 24;
                worksheet.Column(9).Width = 14;

                return package.GetAsByteArray();
            });
        }

        public async Task<byte[]> GenerateRapportDSIDGExcelAsync(List<Projet> projets)
        {
            return await Task.Run(() =>
            {
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Synthese");

                var currentRow = ApplyBrandedHeader(
                    worksheet,
                    "Rapport de gouvernance DSI / DG",
                    $"Edition du {DateTime.UtcNow:dd/MM/yyyy HH:mm}",
                    12);

                worksheet.Cells[currentRow, 1].Value = "SYNTHESE GLOBALE";
                worksheet.Cells[currentRow, 1].Style.Font.Size = 12;
                worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                currentRow++;

                worksheet.Cells[currentRow, 1].Value = "Total Projets";
                worksheet.Cells[currentRow, 2].Value = projets.Count;
                worksheet.Cells[currentRow, 3].Value = "En Cours";
                worksheet.Cells[currentRow, 4].Value = projets.Count(p => p.StatutProjet == StatutProjet.EnCours);
                worksheet.Cells[currentRow, 5].Value = "Clôturés";
                worksheet.Cells[currentRow, 6].Value = projets.Count(p => p.StatutProjet == StatutProjet.Cloture);
                currentRow += 2;

                worksheet.Cells[currentRow, 1].Value = "INDICATEURS RAG";
                worksheet.Cells[currentRow, 1].Style.Font.Size = 12;
                worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                currentRow++;

                worksheet.Cells[currentRow, 1].Value = "Vert";
                worksheet.Cells[currentRow, 2].Value = projets.Count(p => p.IndicateurRAG == IndicateurRAG.Vert);
                worksheet.Cells[currentRow, 3].Value = "Amber";
                worksheet.Cells[currentRow, 4].Value = projets.Count(p => p.IndicateurRAG == IndicateurRAG.Amber);
                worksheet.Cells[currentRow, 5].Value = "Rouge";
                worksheet.Cells[currentRow, 6].Value = projets.Count(p => p.IndicateurRAG == IndicateurRAG.Rouge);
                currentRow += 2;

                worksheet.Cells[currentRow, 1].Value = "DETAIL DES PROJETS";
                worksheet.Cells[currentRow, 1].Style.Font.Size = 12;
                worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                worksheet.Cells[currentRow, 1, currentRow, 12].Merge = true;
                currentRow++;

                var headers = new[]
                {
                    "Code", "Titre", "Direction", "Chef Projet", "RAG", "Phase", "Avancement %",
                    "Statut", "Date Début", "Date Fin Prévue", "Budget Prév.", "Budget Cons."
                };
                WriteHeaderRow(worksheet, currentRow, headers, PrimaryBlue);
                currentRow++;

                foreach (var projet in projets.OrderBy(p => p.IndicateurRAG).ThenBy(p => p.CodeProjet))
                {
                    worksheet.Cells[currentRow, 1].Value = projet.CodeProjet;
                    worksheet.Cells[currentRow, 2].Value = projet.Titre;
                    worksheet.Cells[currentRow, 3].Value = projet.Direction?.Libelle ?? "N/A";
                    worksheet.Cells[currentRow, 4].Value = projet.ChefProjet != null
                        ? $"{projet.ChefProjet.Nom} {projet.ChefProjet.Prenoms}".Trim()
                        : "Non assigné";
                    worksheet.Cells[currentRow, 5].Value = projet.IndicateurRAG.ToString();
                    worksheet.Cells[currentRow, 6].Value = projet.PhaseActuelle.ToString();
                    worksheet.Cells[currentRow, 7].Value = projet.PourcentageAvancementAffiche / 100d;
                    worksheet.Cells[currentRow, 7].Style.Numberformat.Format = "0%";
                    worksheet.Cells[currentRow, 8].Value = projet.StatutProjet.ToString();
                    worksheet.Cells[currentRow, 9].Value = projet.DateDebut?.ToString("dd/MM/yyyy") ?? "N/A";
                    worksheet.Cells[currentRow, 10].Value = projet.DateFinPrevue?.ToString("dd/MM/yyyy") ?? "N/A";
                    worksheet.Cells[currentRow, 11].Value = projet.FicheProjet?.BudgetPrevisionnel ?? 0;
                    worksheet.Cells[currentRow, 11].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[currentRow, 12].Value = projet.FicheProjet?.BudgetConsomme ?? 0;
                    worksheet.Cells[currentRow, 12].Style.Numberformat.Format = "#,##0.00";
                    ApplyTableRowBorder(worksheet, currentRow, headers.Length);
                    currentRow++;
                }

                AutoFitWorksheet(worksheet, 12);
                worksheet.Column(2).Width = 28;
                worksheet.Column(3).Width = 22;
                worksheet.Column(4).Width = 24;

                return package.GetAsByteArray();
            });
        }

        public async Task<byte[]> GeneratePlanningDetailleExcelAsync(Projet projet, IReadOnlyList<TachePlanningProjet> tasks)
        {
            return await Task.Run(() =>
            {
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Planning");

                var currentRow = ApplyBrandedHeader(
                    worksheet,
                    "Planning detaille",
                    $"{projet.CodeProjet} - {projet.Titre}",
                    10);

                currentRow = WriteProjectIdentityBlock(worksheet, projet, currentRow, 10);

                var headers = new[]
                {
                    "WBS", "Tâche", "Responsable", "Début prévu", "Fin prévue", "Durée (jours)", "Avancement (%)", "Jalon", "Dépendances", "Commentaire"
                };
                WriteHeaderRow(worksheet, currentRow, headers, PrimaryBlue);
                currentRow++;

                foreach (var task in tasks.OrderBy(t => t.Ordre))
                {
                    worksheet.Cells[currentRow, 1].Value = task.CodeWbs;
                    worksheet.Cells[currentRow, 2].Value = task.Libelle;
                    worksheet.Cells[currentRow, 3].Value = task.Responsable;
                    worksheet.Cells[currentRow, 4].Value = task.DateDebutPrevue.Date;
                    worksheet.Cells[currentRow, 4].Style.Numberformat.Format = "dd/mm/yyyy";
                    worksheet.Cells[currentRow, 5].Value = task.DateFinPrevue.Date;
                    worksheet.Cells[currentRow, 5].Style.Numberformat.Format = "dd/mm/yyyy";
                    worksheet.Cells[currentRow, 6].Value = Math.Max(1, (task.DateFinPrevue.Date - task.DateDebutPrevue.Date).Days + 1);
                    worksheet.Cells[currentRow, 7].Value = task.Avancement / 100d;
                    worksheet.Cells[currentRow, 7].Style.Numberformat.Format = "0%";
                    worksheet.Cells[currentRow, 8].Value = task.EstJalon ? "Oui" : "Non";
                    worksheet.Cells[currentRow, 9].Value = task.Dependances;
                    worksheet.Cells[currentRow, 10].Value = task.Commentaire;
                    ApplyTableRowBorder(worksheet, currentRow, headers.Length);
                    currentRow++;
                }

                AutoFitWorksheet(worksheet, 10);
                worksheet.Column(2).Width = 34;
                worksheet.Column(9).Width = 18;
                worksheet.Column(10).Width = 30;

                return package.GetAsByteArray();
            });
        }

        public async Task<byte[]> GenerateWbsExcelAsync(Projet projet, IReadOnlyList<TachePlanningProjet> tasks)
        {
            return await Task.Run(() =>
            {
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("WBS");

                var currentRow = ApplyBrandedHeader(
                    worksheet,
                    "WBS / Decoupage des travaux",
                    $"{projet.CodeProjet} - {projet.Titre}",
                    5);

                currentRow = WriteProjectIdentityBlock(worksheet, projet, currentRow, 5);

                var headers = new[] { "WBS", "Libellé", "Responsable", "Type", "Commentaire" };
                WriteHeaderRow(worksheet, currentRow, headers, PrimaryBlue);
                currentRow++;

                foreach (var task in tasks.OrderBy(t => t.Ordre))
                {
                    worksheet.Cells[currentRow, 1].Value = task.CodeWbs;
                    worksheet.Cells[currentRow, 2].Value = task.Libelle;
                    worksheet.Cells[currentRow, 3].Value = task.Responsable;
                    worksheet.Cells[currentRow, 4].Value = task.EstJalon ? "Jalon" : "Tâche";
                    worksheet.Cells[currentRow, 5].Value = task.Commentaire;
                    ApplyTableRowBorder(worksheet, currentRow, headers.Length);
                    currentRow++;
                }

                AutoFitWorksheet(worksheet, 5);
                worksheet.Column(2).Width = 34;
                worksheet.Column(5).Width = 28;

                return package.GetAsByteArray();
            });
        }

        public async Task<byte[]> GenerateMatriceRaciExcelAsync(Projet projet, IReadOnlyList<LigneRaciProjet> lines)
        {
            return await Task.Run(() =>
            {
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("RACI");

                var currentRow = ApplyBrandedHeader(
                    worksheet,
                    "Matrice RACI",
                    $"{projet.CodeProjet} - {projet.Titre}",
                    6);

                currentRow = WriteProjectIdentityBlock(worksheet, projet, currentRow, 6);

                var headers = new[] { "Code", "Activité", "Responsable (R)", "Approbateur (A)", "Consulté (C)", "Informé (I)" };
                WriteHeaderRow(worksheet, currentRow, headers, PrimaryBlue);
                currentRow++;

                foreach (var line in lines.OrderBy(l => l.Ordre))
                {
                    worksheet.Cells[currentRow, 1].Value = line.CodeActivite;
                    worksheet.Cells[currentRow, 2].Value = line.Activite;
                    worksheet.Cells[currentRow, 3].Value = line.Responsable;
                    worksheet.Cells[currentRow, 4].Value = line.Approbateur;
                    worksheet.Cells[currentRow, 5].Value = line.Consulte;
                    worksheet.Cells[currentRow, 6].Value = line.Informe;
                    ApplyTableRowBorder(worksheet, currentRow, headers.Length);
                    currentRow++;
                }

                AutoFitWorksheet(worksheet, 6);
                worksheet.Column(2).Width = 36;
                worksheet.Column(3).Width = 22;
                worksheet.Column(4).Width = 22;
                worksheet.Column(5).Width = 22;
                worksheet.Column(6).Width = 22;

                return package.GetAsByteArray();
            });
        }

        public async Task<byte[]> GenerateSchemaCommunicationExcelAsync(Projet projet, IReadOnlyList<LigneCommunicationProjet> lines)
        {
            return await Task.Run(() =>
            {
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Communication");

                var currentRow = ApplyBrandedHeader(
                    worksheet,
                    "Schema de communication",
                    $"{projet.CodeProjet} - {projet.Titre}",
                    7);

                currentRow = WriteProjectIdentityBlock(worksheet, projet, currentRow, 7);

                var headers = new[] { "Instance", "Objectif", "Fréquence", "Canal", "Participants", "Responsable", "COPIL" };
                WriteHeaderRow(worksheet, currentRow, headers, PrimaryBlue);
                currentRow++;

                foreach (var line in lines.OrderBy(l => l.Ordre))
                {
                    worksheet.Cells[currentRow, 1].Value = line.Instance;
                    worksheet.Cells[currentRow, 2].Value = line.Objectif;
                    worksheet.Cells[currentRow, 3].Value = line.Frequence;
                    worksheet.Cells[currentRow, 4].Value = line.Canal;
                    worksheet.Cells[currentRow, 5].Value = line.Participants;
                    worksheet.Cells[currentRow, 6].Value = line.Responsable;
                    worksheet.Cells[currentRow, 7].Value = line.EstCopil ? "Oui" : "Non";
                    ApplyTableRowBorder(worksheet, currentRow, headers.Length);
                    currentRow++;
                }

                AutoFitWorksheet(worksheet, 7);
                worksheet.Column(2).Width = 34;
                worksheet.Column(5).Width = 32;

                return package.GetAsByteArray();
            });
        }

        public async Task<byte[]> GenerateBudgetPrevisionnelExcelAsync(Projet projet, IReadOnlyList<LigneBudgetPlanificationProjet> lines, FicheProjet? ficheProjet)
        {
            return await Task.Run(() =>
            {
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Budget");

                var currentRow = ApplyBrandedHeader(
                    worksheet,
                    "Budget previsionnel",
                    $"{projet.CodeProjet} - {projet.Titre}",
                    4);

                currentRow = WriteProjectIdentityBlock(worksheet, projet, currentRow, 4);

                var headers = new[] { "Poste", "Description", "Montant (FCFA)", "Commentaire" };
                WriteHeaderRow(worksheet, currentRow, headers, PrimaryBlue);
                currentRow++;

                if (lines.Any())
                {
                    foreach (var line in lines.OrderBy(l => l.Ordre))
                    {
                        worksheet.Cells[currentRow, 1].Value = line.Poste;
                        worksheet.Cells[currentRow, 2].Value = line.Description;
                        worksheet.Cells[currentRow, 3].Value = line.Montant;
                        worksheet.Cells[currentRow, 3].Style.Numberformat.Format = "#,##0.00";
                        worksheet.Cells[currentRow, 4].Value = line.Commentaire;
                        ApplyTableRowBorder(worksheet, currentRow, headers.Length);
                        currentRow++;
                    }

                    worksheet.Cells[currentRow, 1].Value = "TOTAL";
                    worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                    worksheet.Cells[currentRow, 3].Value = lines.Sum(l => l.Montant);
                    worksheet.Cells[currentRow, 3].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[currentRow, 3].Style.Font.Bold = true;
                    ApplyTableRowBorder(worksheet, currentRow, headers.Length);
                    currentRow += 2;
                }
                else if (ficheProjet?.BudgetPrevisionnel.HasValue == true)
                {
                    worksheet.Cells[currentRow, 1].Value = "Budget global";
                    worksheet.Cells[currentRow, 2].Value = "Synthèse budgétaire";
                    worksheet.Cells[currentRow, 3].Value = ficheProjet.BudgetPrevisionnel.Value;
                    worksheet.Cells[currentRow, 3].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[currentRow, 4].Value = ficheProjet.CommentaireBudgetPlanification ?? "";
                    ApplyTableRowBorder(worksheet, currentRow, headers.Length);
                    currentRow++;
                }

                AutoFitWorksheet(worksheet, 4);
                worksheet.Column(2).Width = 36;
                worksheet.Column(4).Width = 28;

                return package.GetAsByteArray();
            });
        }

        public async Task<byte[]> GeneratePvKickOffExcelAsync(Projet projet, PvKickOffProjet kickOff)
        {
            return await Task.Run(() =>
            {
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("PV KickOff");

                var currentRow = ApplyBrandedHeader(
                    worksheet,
                    "Proces-verbal de kick-off",
                    $"{projet.CodeProjet} - {projet.Titre}",
                    2);

                currentRow = WriteProjectIdentityBlock(worksheet, projet, currentRow, 2);

                var rows = new List<(string Label, string Value)>
                {
                    ("Date", kickOff.DateReunion?.ToString("dd/MM/yyyy") ?? ""),
                    ("Heure", kickOff.Heure ?? ""),
                    ("Lieu", kickOff.Lieu ?? ""),
                    ("Animateur", kickOff.Animateur ?? ""),
                    ("Objectifs", kickOff.Objectifs ?? ""),
                    ("Participants", kickOff.Participants ?? ""),
                    ("Ordre du jour", kickOff.OrdreDuJour ?? ""),
                    ("Décisions", kickOff.Decisions ?? ""),
                    ("Actions", kickOff.Actions ?? ""),
                    ("Commentaires", kickOff.Commentaires ?? "")
                };

                WriteHeaderRow(worksheet, currentRow, new[] { "Champ", "Valeur" }, PrimaryBlue);
                currentRow++;

                foreach (var (label, value) in rows)
                {
                    worksheet.Cells[currentRow, 1].Value = label;
                    worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                    worksheet.Cells[currentRow, 2].Value = value;
                    worksheet.Cells[currentRow, 2].Style.WrapText = true;
                    ApplyTableRowBorder(worksheet, currentRow, 2);
                    currentRow++;
                }

                AutoFitWorksheet(worksheet, 2);
                worksheet.Column(1).Width = 22;
                worksheet.Column(2).Width = 65;

                return package.GetAsByteArray();
            });
        }

        private static int ApplyBrandedHeader(ExcelWorksheet worksheet, string title, string subtitle, int endColumn)
        {
            worksheet.View.ShowGridLines = false;
            worksheet.Row(1).Height = 24;
            worksheet.Row(2).Height = 20;
            worksheet.Row(3).Height = 28;
            worksheet.Row(4).Height = 20;
            worksheet.Row(6).Height = 18;

            var logoPath = DocumentBrandingHelper.GetLogoAbsolutePath();
            var textStartColumn = 1;

            if (!string.IsNullOrWhiteSpace(logoPath))
            {
                var picture = worksheet.Drawings.AddPicture($"logo_{worksheet.Name}_{Guid.NewGuid():N}", new FileInfo(logoPath));
                picture.SetPosition(0, 4, 0, 4);
                picture.SetSize(84, 84);
                textStartColumn = 2;
            }

            var lastColumn = Math.Max(endColumn, textStartColumn + 2);

            worksheet.Cells[1, textStartColumn].Value = DocumentBrandingHelper.CompanyName.ToUpperInvariant();
            worksheet.Cells[1, textStartColumn].Style.Font.Bold = true;
            worksheet.Cells[1, textStartColumn].Style.Font.Size = 15;
            worksheet.Cells[1, textStartColumn, 1, lastColumn].Merge = true;

            worksheet.Cells[2, textStartColumn].Value = $"{DocumentBrandingHelper.CompanySite} • {DocumentBrandingHelper.ApplicationName}";
            worksheet.Cells[2, textStartColumn].Style.Font.Size = 10;
            worksheet.Cells[2, textStartColumn].Style.Font.Color.SetColor(Color.DimGray);
            worksheet.Cells[2, textStartColumn, 2, lastColumn].Merge = true;

            worksheet.Cells[3, textStartColumn].Value = title;
            worksheet.Cells[3, textStartColumn].Style.Font.Bold = true;
            worksheet.Cells[3, textStartColumn].Style.Font.Size = 18;
            worksheet.Cells[3, textStartColumn].Style.Font.Color.SetColor(PrimaryBlue);
            worksheet.Cells[3, textStartColumn, 3, lastColumn].Merge = true;

            worksheet.Cells[4, textStartColumn].Value = subtitle;
            worksheet.Cells[4, textStartColumn].Style.Font.Size = 11;
            worksheet.Cells[4, textStartColumn].Style.Font.Color.SetColor(Color.DimGray);
            worksheet.Cells[4, textStartColumn, 4, lastColumn].Merge = true;

            worksheet.Cells[6, textStartColumn].Value = $"Document généré le {DateTime.UtcNow:dd/MM/yyyy à HH:mm}";
            worksheet.Cells[6, textStartColumn].Style.Font.Size = 9;
            worksheet.Cells[6, textStartColumn].Style.Font.Color.SetColor(Color.Gray);
            worksheet.Cells[6, textStartColumn, 6, lastColumn].Merge = true;

            return 8;
        }

        private static int WriteProjectIdentityBlock(ExcelWorksheet worksheet, Projet projet, int startRow, int lastColumn)
        {
            worksheet.Cells[startRow, 1].Value = "Projet";
            worksheet.Cells[startRow, 1].Style.Font.Bold = true;
            worksheet.Cells[startRow, 2].Value = $"{projet.CodeProjet} - {projet.Titre}";
            worksheet.Cells[startRow + 1, 1].Value = "Direction";
            worksheet.Cells[startRow + 1, 1].Style.Font.Bold = true;
            worksheet.Cells[startRow + 1, 2].Value = projet.Direction?.Libelle ?? "N/A";
            worksheet.Cells[startRow + 2, 1].Value = "Chef de projet";
            worksheet.Cells[startRow + 2, 1].Style.Font.Bold = true;
            worksheet.Cells[startRow + 2, 2].Value = projet.ChefProjet != null
                ? $"{projet.ChefProjet.Nom} {projet.ChefProjet.Prenoms}".Trim()
                : "Non assigné";
            worksheet.Cells[startRow + 3, 1].Value = "Sponsor";
            worksheet.Cells[startRow + 3, 1].Style.Font.Bold = true;
            worksheet.Cells[startRow + 3, 2].Value = projet.Sponsor != null
                ? $"{projet.Sponsor.Nom} {projet.Sponsor.Prenoms}".Trim()
                : "N/A";

            using var range = worksheet.Cells[startRow, 1, startRow + 3, Math.Max(2, lastColumn)];
            range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

            return startRow + 6;
        }

        private static int AddMergedSection(ExcelWorksheet worksheet, int row, int startColumn, int endColumn, string title, Color background)
        {
            worksheet.Cells[row, startColumn].Value = title;
            worksheet.Cells[row, startColumn, row, endColumn].Merge = true;
            worksheet.Cells[row, startColumn].Style.Font.Size = 12;
            worksheet.Cells[row, startColumn].Style.Font.Bold = true;
            worksheet.Cells[row, startColumn].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, startColumn].Style.Fill.BackgroundColor.SetColor(background);
            worksheet.Cells[row, startColumn].Style.Font.Color.SetColor(Color.White);
            return row + 1;
        }

        private static void WriteHeaderRow(ExcelWorksheet worksheet, int row, IReadOnlyList<string> headers, Color background)
        {
            for (var index = 0; index < headers.Count; index++)
            {
                var cell = worksheet.Cells[row, index + 1];
                cell.Value = headers[index];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(background);
                cell.Style.Font.Color.SetColor(Color.White);
                cell.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                cell.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                cell.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            }
        }

        private static void ApplyTableRowBorder(ExcelWorksheet worksheet, int row, int columnCount)
        {
            for (var column = 1; column <= columnCount; column++)
            {
                var cell = worksheet.Cells[row, column];
                cell.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                cell.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                cell.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            }
        }

        private static void AutoFitWorksheet(ExcelWorksheet worksheet, int maxColumn)
        {
            if (worksheet.Dimension == null)
                return;

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
            for (var column = 1; column <= maxColumn; column++)
            {
                worksheet.Column(column).Width = Math.Min(worksheet.Column(column).Width + 2, 65);
            }
        }
    }
}
