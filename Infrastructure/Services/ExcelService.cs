using GestionProjects.Domain.Models;
using GestionProjects.Domain.Enums;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace GestionProjects.Infrastructure.Services
{
    public interface IExcelService
    {
        Task<byte[]> GeneratePortefeuilleProjetsExcelAsync(PortefeuilleProjet portefeuille, List<Projet> projets);
        Task<byte[]> GenerateRapportDSIDGExcelAsync(List<Projet> projets);
    }

    public class ExcelService : IExcelService
    {
        public ExcelService()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public async Task<byte[]> GeneratePortefeuilleProjetsExcelAsync(PortefeuilleProjet portefeuille, List<Projet> projets)
        {
            return await Task.Run(() =>
            {
                using (var package = new ExcelPackage())
                {
                    // Feuille 1 : Vue d'ensemble
                    var worksheet = package.Workbook.Worksheets.Add("Portefeuille");

                    // En-tête avec logo et titre
                    worksheet.Cells[1, 1].Value = "CÔTE D'IVOIRE TERMINAL";
                    worksheet.Cells[1, 1].Style.Font.Size = 14;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[2, 1].Value = "ABIDJAN";
                    worksheet.Cells[2, 1].Style.Font.Size = 12;
                    worksheet.Cells[3, 1].Value = portefeuille.Nom ?? "Portefeuille de Projet DSI";
                    worksheet.Cells[3, 1].Style.Font.Size = 16;
                    worksheet.Cells[3, 1].Style.Font.Bold = true;
                    worksheet.Cells[3, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(52, 129, 192));

                    int currentRow = 5;

                    // Objectif Stratégique Global
                    worksheet.Cells[currentRow, 1].Value = "OBJECTIF STRATÉGIQUE GLOBAL";
                    worksheet.Cells[currentRow, 1].Style.Font.Size = 12;
                    worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                    worksheet.Cells[currentRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[currentRow, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(52, 129, 192));
                    worksheet.Cells[currentRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[currentRow, 1, currentRow, 3].Merge = true;
                    currentRow++;
                    worksheet.Cells[currentRow, 1].Value = portefeuille.ObjectifStrategiqueGlobal ?? "";
                    worksheet.Cells[currentRow, 1, currentRow, 3].Merge = true;
                    worksheet.Cells[currentRow, 1].Style.WrapText = true;
                    currentRow += 2;

                    // Avantages Attendus
                    worksheet.Cells[currentRow, 1].Value = "AVANTAGES ATTENDUS DU PORTEFEUILLE";
                    worksheet.Cells[currentRow, 1].Style.Font.Size = 12;
                    worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                    worksheet.Cells[currentRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[currentRow, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(34, 197, 94));
                    worksheet.Cells[currentRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[currentRow, 1, currentRow, 3].Merge = true;
                    currentRow++;
                    if (!string.IsNullOrWhiteSpace(portefeuille.AvantagesAttendus))
                    {
                        var avantages = portefeuille.AvantagesAttendus.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var avantage in avantages)
                        {
                            var ligne = avantage.Trim();
                            if (ligne.StartsWith("•"))
                                ligne = ligne.Substring(1).Trim();
                            worksheet.Cells[currentRow, 1].Value = $"• {ligne}";
                            worksheet.Cells[currentRow, 1, currentRow, 3].Merge = true;
                            currentRow++;
                        }
                    }
                    currentRow++;

                    // Risques et Mitigations
                    worksheet.Cells[currentRow, 1].Value = "RISQUES ET MITIGATIONS GLOBAUX";
                    worksheet.Cells[currentRow, 1].Style.Font.Size = 12;
                    worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                    worksheet.Cells[currentRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[currentRow, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(249, 115, 22));
                    worksheet.Cells[currentRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    worksheet.Cells[currentRow, 1, currentRow, 3].Merge = true;
                    currentRow++;
                    if (!string.IsNullOrWhiteSpace(portefeuille.RisquesEtMitigations))
                    {
                        var risques = portefeuille.RisquesEtMitigations.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var risque in risques)
                        {
                            var ligne = risque.Trim();
                            if (!string.IsNullOrWhiteSpace(ligne))
                            {
                                var parts = ligne.Split(':', 2);
                                if (parts.Length == 2)
                                {
                                    worksheet.Cells[currentRow, 1].Value = parts[0].Trim();
                                    worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                                    worksheet.Cells[currentRow, 2].Value = $"Mitigation: {parts[1].Trim()}";
                                    worksheet.Cells[currentRow, 2, currentRow, 3].Merge = true;
                                    worksheet.Cells[currentRow, 2].Style.WrapText = true;
                                }
                                else
                                {
                                    worksheet.Cells[currentRow, 1].Value = ligne;
                                    worksheet.Cells[currentRow, 1, currentRow, 3].Merge = true;
                                }
                                currentRow++;
                            }
                        }
                    }
                    currentRow += 2;

                    // Tableau des projets
                    worksheet.Cells[currentRow, 1].Value = "PROJETS INCLUS DANS LE PORTEFEUILLE";
                    worksheet.Cells[currentRow, 1].Style.Font.Size = 14;
                    worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                    worksheet.Cells[currentRow, 1, currentRow, 8].Merge = true;
                    currentRow++;

                    // En-têtes du tableau
                    var headerRow = currentRow;
                    worksheet.Cells[headerRow, 1].Value = "#";
                    worksheet.Cells[headerRow, 2].Value = "Code Projet";
                    worksheet.Cells[headerRow, 3].Value = "Titre";
                    worksheet.Cells[headerRow, 4].Value = "Objectif";
                    worksheet.Cells[headerRow, 5].Value = "Sponsor";
                    worksheet.Cells[headerRow, 6].Value = "Chef Projet";
                    worksheet.Cells[headerRow, 7].Value = "Statut";
                    worksheet.Cells[headerRow, 8].Value = "Phase";
                    worksheet.Cells[headerRow, 9].Value = "Avancement %";

                    // Style des en-têtes
                    for (int col = 1; col <= 9; col++)
                    {
                        worksheet.Cells[headerRow, col].Style.Font.Bold = true;
                        worksheet.Cells[headerRow, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[headerRow, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(240, 240, 240));
                        worksheet.Cells[headerRow, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    }

                    currentRow++;
                    int index = 1;
                    foreach (var projet in projets)
                    {
                        worksheet.Cells[currentRow, 1].Value = index;
                        worksheet.Cells[currentRow, 2].Value = projet.CodeProjet;
                        worksheet.Cells[currentRow, 3].Value = projet.Titre;
                        worksheet.Cells[currentRow, 4].Value = projet.Objectif ?? "";
                        worksheet.Cells[currentRow, 4].Style.WrapText = true;
                        worksheet.Cells[currentRow, 5].Value = $"{projet.Sponsor?.Nom} {projet.Sponsor?.Prenoms}";
                        worksheet.Cells[currentRow, 6].Value = $"{projet.ChefProjet?.Nom} {projet.ChefProjet?.Prenoms ?? "Non assigné"}";
                        worksheet.Cells[currentRow, 7].Value = projet.StatutProjet.ToString();
                        worksheet.Cells[currentRow, 8].Value = projet.PhaseActuelle.ToString();
                        worksheet.Cells[currentRow, 9].Value = projet.PourcentageAvancement;
                        worksheet.Cells[currentRow, 9].Style.Numberformat.Format = "0%";

                        // Bordures
                        for (int col = 1; col <= 9; col++)
                        {
                            worksheet.Cells[currentRow, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                        }

                        currentRow++;
                        index++;
                    }

                    // Ajuster la largeur des colonnes
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                    worksheet.Column(1).Width = 5;
                    worksheet.Column(2).Width = 15;
                    worksheet.Column(3).Width = 30;
                    worksheet.Column(4).Width = 40;
                    worksheet.Column(5).Width = 25;
                    worksheet.Column(6).Width = 25;
                    worksheet.Column(7).Width = 15;
                    worksheet.Column(8).Width = 20;
                    worksheet.Column(9).Width = 12;

                    // Ajouter la date de génération
                    worksheet.Cells[currentRow + 2, 1].Value = $"Document généré le {DateTime.Now:dd/MM/yyyy à HH:mm}";
                    worksheet.Cells[currentRow + 2, 1].Style.Font.Size = 9;
                    worksheet.Cells[currentRow + 2, 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);

                    return package.GetAsByteArray();
                }
            });
        }

        public async Task<byte[]> GenerateRapportDSIDGExcelAsync(List<Projet> projets)
        {
            return await Task.Run(() =>
            {
                using (var package = new ExcelPackage())
                {
                    // Feuille 1 : Synthèse
                    var worksheet = package.Workbook.Worksheets.Add("Synthèse");

                    // En-tête
                    worksheet.Cells[1, 1].Value = "RAPPORT DE GOUVERNANCE DSI/DG";
                    worksheet.Cells[1, 1].Style.Font.Size = 16;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1, 1, 6].Merge = true;

                    worksheet.Cells[2, 1].Value = $"Date du rapport: {DateTime.Now:dd/MM/yyyy HH:mm}";
                    worksheet.Cells[2, 1, 2, 6].Merge = true;

                    int currentRow = 4;

                    // Synthèse globale
                    worksheet.Cells[currentRow, 1].Value = "SYNTHÈSE GLOBALE";
                    worksheet.Cells[currentRow, 1].Style.Font.Size = 12;
                    worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                    currentRow++;

                    worksheet.Cells[currentRow, 1].Value = "Total Projets";
                    worksheet.Cells[currentRow, 2].Value = projets.Count;
                    worksheet.Cells[currentRow, 3].Value = "En Cours";
                    worksheet.Cells[currentRow, 4].Value = projets.Count(p => p.StatutProjet == StatutProjet.EnCours);
                    worksheet.Cells[currentRow, 5].Value = "Clôturés";
                    worksheet.Cells[currentRow, 6].Value = projets.Count(p => p.StatutProjet == StatutProjet.Cloture);
                    currentRow++;

                    // Indicateurs RAG
                    worksheet.Cells[currentRow, 1].Value = "INDICATEURS RAG";
                    worksheet.Cells[currentRow, 1].Style.Font.Size = 12;
                    worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                    currentRow++;

                    worksheet.Cells[currentRow, 1].Value = "🟢 Vert";
                    worksheet.Cells[currentRow, 2].Value = projets.Count(p => p.IndicateurRAG == IndicateurRAG.Vert);
                    worksheet.Cells[currentRow, 3].Value = "🟡 Amber";
                    worksheet.Cells[currentRow, 4].Value = projets.Count(p => p.IndicateurRAG == IndicateurRAG.Amber);
                    worksheet.Cells[currentRow, 5].Value = "🔴 Rouge";
                    worksheet.Cells[currentRow, 6].Value = projets.Count(p => p.IndicateurRAG == IndicateurRAG.Rouge);
                    currentRow += 2;

                    // Détail des projets
                    worksheet.Cells[currentRow, 1].Value = "DÉTAIL DES PROJETS";
                    worksheet.Cells[currentRow, 1].Style.Font.Size = 12;
                    worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
                    worksheet.Cells[currentRow, 1, currentRow, 6].Merge = true;
                    currentRow++;

                    // En-têtes du tableau
                    var headers = new[] { "Code", "Titre", "Direction", "Chef Projet", "RAG", "Phase", "Avancement %", "Statut", "Date Début", "Date Fin Prévue", "Budget Prév.", "Budget Cons." };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cells[currentRow, i + 1].Value = headers[i];
                        worksheet.Cells[currentRow, i + 1].Style.Font.Bold = true;
                        worksheet.Cells[currentRow, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[currentRow, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(52, 129, 192));
                        worksheet.Cells[currentRow, i + 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    }
                    currentRow++;

                    // Données des projets
                    foreach (var projet in projets.OrderBy(p => p.IndicateurRAG).ThenBy(p => p.CodeProjet))
                    {
                        var ragLabel = projet.IndicateurRAG switch
                        {
                            IndicateurRAG.Vert => "🟢 Vert",
                            IndicateurRAG.Amber => "🟡 Amber",
                            IndicateurRAG.Rouge => "🔴 Rouge",
                            _ => "N/A"
                        };

                        worksheet.Cells[currentRow, 1].Value = projet.CodeProjet;
                        worksheet.Cells[currentRow, 2].Value = projet.Titre;
                        worksheet.Cells[currentRow, 3].Value = projet.Direction?.Libelle ?? "N/A";
                        worksheet.Cells[currentRow, 4].Value = projet.ChefProjet != null ? $"{projet.ChefProjet.Nom} {projet.ChefProjet.Prenoms}" : "Non assigné";
                        worksheet.Cells[currentRow, 5].Value = ragLabel;
                        worksheet.Cells[currentRow, 6].Value = projet.PhaseActuelle.ToString();
                        worksheet.Cells[currentRow, 7].Value = projet.PourcentageAvancement;
                        worksheet.Cells[currentRow, 8].Value = projet.StatutProjet.ToString();
                        worksheet.Cells[currentRow, 9].Value = projet.DateDebut?.ToString("dd/MM/yyyy") ?? "N/A";
                        worksheet.Cells[currentRow, 10].Value = projet.DateFinPrevue?.ToString("dd/MM/yyyy") ?? "N/A";
                        
                        // Budget depuis FicheProjet
                        var ficheProjet = projet.FicheProjet;
                        worksheet.Cells[currentRow, 11].Value = ficheProjet?.BudgetPrevisionnel ?? 0;
                        worksheet.Cells[currentRow, 12].Value = ficheProjet?.BudgetConsomme ?? 0;
                        
                        // Formatage des cellules de budget
                        if (ficheProjet?.BudgetPrevisionnel.HasValue == true)
                        {
                            worksheet.Cells[currentRow, 11].Style.Numberformat.Format = "#,##0.00";
                        }
                        if (ficheProjet?.BudgetConsomme.HasValue == true)
                        {
                            worksheet.Cells[currentRow, 12].Style.Numberformat.Format = "#,##0.00";
                        }
                        
                        currentRow++;
                    }

                    // Ajuster la largeur des colonnes
                    worksheet.Cells.AutoFitColumns();

                    return package.GetAsByteArray();
                }
            });
        }
    }
}

