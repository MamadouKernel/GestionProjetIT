using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using GestionProjects.Domain.Models;
using System.Text;

namespace GestionProjects.Infrastructure.Services
{
    public interface IWordService
    {
        Task<byte[]> GenerateCharteProjetWordAsync(CharteProjet charte);
        Task<byte[]> GenerateFicheProjetWordAsync(FicheProjet fiche);
    }

    public class WordService : IWordService
    {
        public async Task<byte[]> GenerateCharteProjetWordAsync(CharteProjet charte)
        {
            return await Task.Run(() =>
            {
                using (var stream = new MemoryStream())
                {
                    using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
                    {
                        var mainPart = wordDocument.AddMainDocumentPart();
                        mainPart.Document = new Document();
                        var body = mainPart.Document.AppendChild(new Body());

                        // Fonction helper pour créer un paragraphe
                        Paragraph CreateParagraph(string text, bool bold = false, int fontSize = 11, string alignment = "left")
                        {
                            var para = new Paragraph();
                            var run = new Run();
                            var runProperties = new RunProperties();
                            if (bold)
                                runProperties.AppendChild(new Bold());
                            runProperties.AppendChild(new FontSize { Val = (fontSize * 2).ToString() });
                            run.AppendChild(runProperties);
                            run.AppendChild(new Text(text));
                            para.AppendChild(run);
                            
                            var paraProps = new ParagraphProperties();
                            if (alignment == "center")
                                paraProps.AppendChild(new Justification { Val = JustificationValues.Center });
                            else if (alignment == "right")
                                paraProps.AppendChild(new Justification { Val = JustificationValues.Right });
                            para.PrependChild(paraProps);
                            
                            return para;
                        }

                        // Fonction helper pour créer un paragraphe avec plusieurs runs
                        Paragraph CreateParagraphWithRuns(params (string text, bool bold, int fontSize)[] runs)
                        {
                            var para = new Paragraph();
                            foreach (var (text, bold, fontSize) in runs)
                            {
                                var run = new Run();
                                var runProperties = new RunProperties();
                                if (bold)
                                    runProperties.AppendChild(new Bold());
                                runProperties.AppendChild(new FontSize { Val = (fontSize * 2).ToString() });
                                run.AppendChild(runProperties);
                                run.AppendChild(new Text(text));
                                para.AppendChild(run);
                            }
                            return para;
                        }

                        // En-tête
                        body.AppendChild(CreateParagraph(charte.Departement ?? "", false, 10));
                        body.AppendChild(CreateParagraph(charte.TypeDocument ?? "", true, 12));
                        body.AppendChild(CreateParagraph(charte.CodeDocument ?? "", false, 9));
                        body.AppendChild(new Paragraph(new Run(new Text("")))); // Ligne vide

                        // Titre
                        body.AppendChild(CreateParagraph("CHARTE DU PROJET", true, 18, "center"));
                        body.AppendChild(CreateParagraph(charte.NomProjet ?? "", true, 14));
                        body.AppendChild(new Paragraph(new Run(new Text(""))));

                        // Identification du projet
                        body.AppendChild(CreateParagraph("IDENTIFICATION DU PROJET", true, 12));
                        body.AppendChild(CreateParagraphWithRuns(("Nom du Projet: ", true, 11), (charte.NomProjet ?? "N/A", false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Numéro du projet: ", true, 11), (charte.NumeroProjet ?? "N/A", false, 11)));
                        body.AppendChild(new Paragraph(new Run(new Text(""))));
                        body.AppendChild(CreateParagraph("Objectif du projet:", true, 11));
                        
                        if (!string.IsNullOrWhiteSpace(charte.ObjectifProjet))
                        {
                            foreach (var objectif in charte.ObjectifProjet.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                            {
                                body.AppendChild(CreateParagraph($"• {objectif.Trim()}", false, 11));
                            }
                        }

                        // Assurance qualité
                        if (!string.IsNullOrWhiteSpace(charte.AssuranceQualite))
                        {
                            body.AppendChild(new Paragraph(new Run(new Text(""))));
                            body.AppendChild(CreateParagraph("Assurance de la qualité:", true, 11));
                            foreach (var ligne in charte.AssuranceQualite.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                            {
                                body.AppendChild(CreateParagraph($"• {ligne.Trim()}", false, 11));
                            }
                        }

                        // Périmètre
                        if (!string.IsNullOrWhiteSpace(charte.Perimetre))
                        {
                            body.AppendChild(new Paragraph(new Run(new Text(""))));
                            body.AppendChild(CreateParagraph("Périmètre:", true, 11));
                            foreach (var ligne in charte.Perimetre.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                            {
                                body.AppendChild(CreateParagraph($"• {ligne.Trim()}", false, 11));
                            }
                        }

                        // Contraintes
                        if (!string.IsNullOrWhiteSpace(charte.ContraintesInitiales))
                        {
                            body.AppendChild(new Paragraph(new Run(new Text(""))));
                            body.AppendChild(CreateParagraph("Contraintes Initiales:", true, 11));
                            foreach (var ligne in charte.ContraintesInitiales.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                            {
                                body.AppendChild(CreateParagraph($"• {ligne.Trim()}", false, 11));
                            }
                        }

                        // Risques
                        if (!string.IsNullOrWhiteSpace(charte.RisquesInitiaux))
                        {
                            body.AppendChild(new Paragraph(new Run(new Text(""))));
                            body.AppendChild(CreateParagraph("Risques Initiaux:", true, 11));
                            foreach (var ligne in charte.RisquesInitiaux.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                            {
                                body.AppendChild(CreateParagraph($"• {ligne.Trim()}", false, 11));
                            }
                        }

                        // Acteurs
                        body.AppendChild(new Paragraph(new Run(new Text(""))));
                        body.AppendChild(CreateParagraph("ACTEURS DU PROJET", true, 12));
                        body.AppendChild(CreateParagraphWithRuns(("Demandeur: ", true, 11), ($"{charte.Demandeur?.Nom} {charte.Demandeur?.Prenoms}", false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Sponsors: ", true, 11), (charte.Sponsors ?? "N/A", false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Chef de Projet: ", true, 11), ($"{charte.ChefProjet?.Nom} {charte.ChefProjet?.Prenoms}", false, 11)));
                        if (!string.IsNullOrWhiteSpace(charte.EmailChefProjet))
                        {
                            body.AppendChild(CreateParagraphWithRuns(("Email: ", true, 11), (charte.EmailChefProjet, false, 11)));
                        }

                        // Jalons
                        if (charte.Jalons != null && charte.Jalons.Any(j => !j.EstSupprime))
                        {
                            body.AppendChild(new Paragraph(new Run(new Text(""))));
                            body.AppendChild(CreateParagraph("ÉLÉMENTS DE PLANIFICATION PRÉVISIONNELLE", true, 12));
                            foreach (var jalon in charte.Jalons.Where(j => !j.EstSupprime).OrderBy(j => j.Ordre))
                            {
                                body.AppendChild(CreateParagraph(jalon.Nom, true, 11));
                                body.AppendChild(CreateParagraphWithRuns(("Description: ", true, 11), (jalon.Description, false, 11)));
                                body.AppendChild(CreateParagraphWithRuns(("Critères d'Approbation: ", true, 11), (jalon.CriteresApprobation, false, 11)));
                                if (jalon.DatePrevisionnelle.HasValue)
                                {
                                    body.AppendChild(CreateParagraphWithRuns(("Date prévisionnelle: ", true, 11), (jalon.DatePrevisionnelle.Value.ToString("dd/MM/yyyy"), false, 11)));
                                }
                                body.AppendChild(new Paragraph(new Run(new Text(""))));
                            }
                        }

                        // Parties prenantes
                        if (charte.PartiesPrenantes != null && charte.PartiesPrenantes.Any(p => !p.EstSupprime))
                        {
                            body.AppendChild(new Paragraph(new Run(new Text(""))));
                            body.AppendChild(CreateParagraph("PARTIES PRENANTES", true, 12));
                            foreach (var partie in charte.PartiesPrenantes.Where(p => !p.EstSupprime))
                            {
                                body.AppendChild(CreateParagraph($"{partie.Nom} - {partie.Role}", false, 11));
                            }
                        }

                        // Autorisation officielle
                        body.AppendChild(new Paragraph(new Run(new Text(""))));
                        body.AppendChild(CreateParagraph("AUTORISATION OFFICIELLE", true, 12));
                        body.AppendChild(CreateParagraph("Nous, les soussignés, approuvons le lancement du projet.", false, 11));
                        body.AppendChild(new Paragraph(new Run(new Text(""))));

                        // Signatures
                        var signatureTable = new Table();
                        var signatureRow = new TableRow();
                        
                        // Colonne Sponsor
                        var sponsorCell = new TableCell();
                        sponsorCell.AppendChild(CreateParagraph("Signature Sponsor", true, 11));
                        if (charte.SignatureSponsor && charte.DateSignatureSponsor.HasValue)
                        {
                            sponsorCell.AppendChild(CreateParagraph($"✓ Signé le {charte.DateSignatureSponsor.Value:dd/MM/yyyy}", false, 11));
                        }
                        else
                        {
                            sponsorCell.AppendChild(CreateParagraph("__________________", false, 11));
                        }
                        signatureRow.AppendChild(sponsorCell);

                        // Colonne Chef de Projet
                        var chefCell = new TableCell();
                        chefCell.AppendChild(CreateParagraph("Signature Chef de Projet", true, 11));
                        if (charte.SignatureChefProjet && charte.DateSignatureChefProjet.HasValue)
                        {
                            chefCell.AppendChild(CreateParagraph($"✓ Signé le {charte.DateSignatureChefProjet.Value:dd/MM/yyyy}", false, 11));
                        }
                        else
                        {
                            chefCell.AppendChild(CreateParagraph("__________________", false, 11));
                        }
                        signatureRow.AppendChild(chefCell);

                        signatureTable.AppendChild(signatureRow);
                        body.AppendChild(signatureTable);

                        // Pied de page
                        body.AppendChild(new Paragraph(new Run(new Text(""))));
                        body.AppendChild(CreateParagraph($"Document généré le {DateTime.Now:dd/MM/yyyy à HH:mm}", false, 8, "center"));
                    }

                    return stream.ToArray();
                }
            });
        }

        public async Task<byte[]> GenerateFicheProjetWordAsync(FicheProjet fiche)
        {
            return await Task.Run(() =>
            {
                using (var stream = new MemoryStream())
                {
                    using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
                    {
                        var mainPart = wordDocument.AddMainDocumentPart();
                        mainPart.Document = new Document();
                        var body = mainPart.Document.AppendChild(new Body());

                        // Fonction helper pour créer un paragraphe
                        Paragraph CreateParagraph(string text, bool bold = false, int fontSize = 11, string alignment = "left")
                        {
                            var para = new Paragraph();
                            var run = new Run();
                            var runProperties = new RunProperties();
                            if (bold)
                                runProperties.AppendChild(new Bold());
                            runProperties.AppendChild(new FontSize { Val = (fontSize * 2).ToString() });
                            run.AppendChild(runProperties);
                            run.AppendChild(new Text(text));
                            para.AppendChild(run);
                            
                            var paraProps = new ParagraphProperties();
                            if (alignment == "center")
                                paraProps.AppendChild(new Justification { Val = JustificationValues.Center });
                            para.PrependChild(paraProps);
                            
                            return para;
                        }

                        Paragraph CreateParagraphWithRuns(params (string text, bool bold, int fontSize)[] runs)
                        {
                            var para = new Paragraph();
                            foreach (var (text, bold, fontSize) in runs)
                            {
                                var run = new Run();
                                var runProperties = new RunProperties();
                                if (bold)
                                    runProperties.AppendChild(new Bold());
                                runProperties.AppendChild(new FontSize { Val = (fontSize * 2).ToString() });
                                run.AppendChild(runProperties);
                                run.AppendChild(new Text(text));
                                para.AppendChild(run);
                            }
                            return para;
                        }

                        // Titre
                        body.AppendChild(CreateParagraph("FICHE PROJET CIT", true, 18, "center"));
                        body.AppendChild(new Paragraph(new Run(new Text(""))));

                        // 1. Identification
                        body.AppendChild(CreateParagraph("1. IDENTIFICATION DU PROJET", true, 14));
                        body.AppendChild(CreateParagraphWithRuns(("Code projet: ", true, 11), (fiche.Projet?.CodeProjet ?? "N/A", false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Titre court: ", true, 11), (fiche.TitreCourt ?? fiche.Projet?.Titre ?? "N/A", false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Titre long: ", true, 11), (fiche.TitreLong ?? fiche.Projet?.Titre ?? "N/A", false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Direction: ", true, 11), (fiche.Projet?.Direction?.Libelle ?? "N/A", false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Demandeur: ", true, 11), ($"{fiche.Projet?.DemandeProjet?.Demandeur?.Nom} {fiche.Projet?.DemandeProjet?.Demandeur?.Prenoms}", false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Sponsor: ", true, 11), ($"{fiche.Projet?.Sponsor?.Nom} {fiche.Projet?.Sponsor?.Prenoms}", false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Chef de Projet DSI: ", true, 11), ($"{fiche.Projet?.ChefProjet?.Nom} {fiche.Projet?.ChefProjet?.Prenoms ?? "Non assigné"}", false, 11)));
                        body.AppendChild(new Paragraph(new Run(new Text(""))));

                        // 2. Objectifs & Description
                        body.AppendChild(CreateParagraph("2. OBJECTIFS & DESCRIPTION", true, 14));
                        if (!string.IsNullOrWhiteSpace(fiche.ObjectifPrincipal))
                        {
                            body.AppendChild(CreateParagraph("Objectif principal:", true, 11));
                            body.AppendChild(CreateParagraph(fiche.ObjectifPrincipal, false, 11));
                        }
                        if (!string.IsNullOrWhiteSpace(fiche.ContexteProblemeAdresse))
                        {
                            body.AppendChild(CreateParagraph("Contexte / Problème adressé:", true, 11));
                            body.AppendChild(CreateParagraph(fiche.ContexteProblemeAdresse, false, 11));
                        }
                        if (!string.IsNullOrWhiteSpace(fiche.DescriptionSynthetique))
                        {
                            body.AppendChild(CreateParagraph("Description synthétique:", true, 11));
                            body.AppendChild(CreateParagraph(fiche.DescriptionSynthetique, false, 11));
                        }
                        if (!string.IsNullOrWhiteSpace(fiche.ResultatsAttendus))
                        {
                            body.AppendChild(CreateParagraph("Résultats attendus:", true, 11));
                            body.AppendChild(CreateParagraph(fiche.ResultatsAttendus, false, 11));
                        }
                        body.AppendChild(new Paragraph(new Run(new Text(""))));

                        // 3. Périmètre
                        if (!string.IsNullOrWhiteSpace(fiche.PerimetreInclus) || !string.IsNullOrWhiteSpace(fiche.PerimetreExclu))
                        {
                            body.AppendChild(CreateParagraph("3. PÉRIMÈTRE", true, 14));
                            if (!string.IsNullOrWhiteSpace(fiche.PerimetreInclus))
                            {
                                body.AppendChild(CreateParagraph("Périmètre inclus (IN):", true, 11));
                                foreach (var ligne in fiche.PerimetreInclus.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                                {
                                    body.AppendChild(CreateParagraph($"• {ligne.Trim()}", false, 11));
                                }
                            }
                            if (!string.IsNullOrWhiteSpace(fiche.PerimetreExclu))
                            {
                                body.AppendChild(CreateParagraph("Périmètre exclu (OUT):", true, 11));
                                foreach (var ligne in fiche.PerimetreExclu.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                                {
                                    body.AppendChild(CreateParagraph($"• {ligne.Trim()}", false, 11));
                                }
                            }
                            body.AppendChild(new Paragraph(new Run(new Text(""))));
                        }

                        // 4. Indicateurs clés
                        body.AppendChild(CreateParagraph("4. INDICATEURS CLÉS", true, 14));
                        if (!string.IsNullOrWhiteSpace(fiche.BeneficesAttendus))
                        {
                            body.AppendChild(CreateParagraph("Bénéfices attendus:", true, 11));
                            body.AppendChild(CreateParagraph(fiche.BeneficesAttendus, false, 11));
                        }
                        body.AppendChild(CreateParagraphWithRuns(("Criticité / Urgence: ", true, 11), (fiche.CriticiteUrgence ?? "N/A", false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Type de projet: ", true, 11), (fiche.TypeProjet ?? "N/A", false, 11)));
                        body.AppendChild(new Paragraph(new Run(new Text(""))));

                        // 5. Planning synthétique
                        body.AppendChild(CreateParagraph("5. PLANNING SYNTHÉTIQUE", true, 14));
                        body.AppendChild(CreateParagraphWithRuns(("Phase actuelle: ", true, 11), (fiche.Projet?.PhaseActuelle.ToString() ?? "Demande", false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Date début: ", true, 11), (fiche.Projet?.DateDebut?.ToString("dd/MM/yyyy") ?? "N/A", false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Date fin prévue: ", true, 11), (fiche.Projet?.DateFinPrevue?.ToString("dd/MM/yyyy") ?? "N/A", false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Prochain jalon: ", true, 11), (fiche.ProchainJalon ?? "N/A", false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Statut: ", true, 11), (fiche.Projet?.EtatProjet.ToString() ?? "Vert", false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("% Avancement: ", true, 11), ($"{fiche.Projet?.PourcentageAvancement ?? 0}%", false, 11)));
                        body.AppendChild(new Paragraph(new Run(new Text(""))));

                        // 6. Principaux risques
                        if (!string.IsNullOrWhiteSpace(fiche.SyntheseRisques))
                        {
                            body.AppendChild(CreateParagraph("6. PRINCIPAUX RISQUES", true, 14));
                            body.AppendChild(CreateParagraph(fiche.SyntheseRisques, false, 11));
                            body.AppendChild(new Paragraph(new Run(new Text(""))));
                        }

                        // 7. Gouvernance
                        body.AppendChild(CreateParagraph("7. GOUVERNANCE ET ACTEURS", true, 14));
                        body.AppendChild(CreateParagraphWithRuns(("Chef de Projet DSI: ", true, 11), ($"{fiche.Projet?.ChefProjet?.Nom} {fiche.Projet?.ChefProjet?.Prenoms ?? "Non assigné"}", false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Sponsor: ", true, 11), ($"{fiche.Projet?.Sponsor?.Nom} {fiche.Projet?.Sponsor?.Prenoms}", false, 11)));
                        if (!string.IsNullOrWhiteSpace(fiche.EquipeProjet))
                        {
                            body.AppendChild(CreateParagraph("Équipe projet:", true, 11));
                            foreach (var ligne in fiche.EquipeProjet.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                            {
                                body.AppendChild(CreateParagraph($"• {ligne.Trim()}", false, 11));
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(fiche.PartiesPrenantesCles))
                        {
                            body.AppendChild(CreateParagraph("Parties prenantes clés:", true, 11));
                            body.AppendChild(CreateParagraph(fiche.PartiesPrenantesCles, false, 11));
                        }
                        body.AppendChild(new Paragraph(new Run(new Text(""))));

                        // 8. Livrables obligatoires
                        body.AppendChild(CreateParagraph("8. LIVRABLES OBLIGATOIRES", true, 14));
                        body.AppendChild(CreateParagraphWithRuns(("Charte Projet: ", true, 11), ((fiche.CharteProjetPresente ? "✔" : "✖"), false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("WBS / Planning / RACI / Budget: ", true, 11), ((fiche.WBSPlanningRACIBudgetPresent ? "✔" : "✖"), false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("CR réunions: ", true, 11), ((fiche.CRReunionsPresent ? "✔" : "✖"), false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Cahier tests / PV Recette / PV MEP: ", true, 11), ((fiche.CahierTestsPVRecettePVMEPPresent ? "✔" : "✖"), false, 11)));
                        body.AppendChild(CreateParagraphWithRuns(("Rapport / Leçons apprises / PV Clôture: ", true, 11), ((fiche.RapportLeconsApprisesPVCloturePresent ? "✔" : "✖"), false, 11)));
                        body.AppendChild(new Paragraph(new Run(new Text(""))));

                        // 9. Budget
                        if (fiche.BudgetPrevisionnel.HasValue || fiche.BudgetConsomme.HasValue)
                        {
                            body.AppendChild(CreateParagraph("9. BUDGET", true, 14));
                            body.AppendChild(CreateParagraphWithRuns(("Budget prévisionnel: ", true, 11), ($"{fiche.BudgetPrevisionnel?.ToString("N2") ?? "N/A"} FCFA", false, 11)));
                            body.AppendChild(CreateParagraphWithRuns(("Budget consommé: ", true, 11), ($"{fiche.BudgetConsomme?.ToString("N2") ?? "N/A"} FCFA", false, 11)));
                            if (fiche.EcartsBudget.HasValue)
                            {
                                body.AppendChild(CreateParagraphWithRuns(("Écarts: ", true, 11), ($"{fiche.EcartsBudget.Value.ToString("N2")} FCFA", false, 11)));
                            }
                            body.AppendChild(new Paragraph(new Run(new Text(""))));
                        }

                        // 10. Synthèse DSI
                        if (!string.IsNullOrWhiteSpace(fiche.PointsForts) || !string.IsNullOrWhiteSpace(fiche.PointsVigilance))
                        {
                            body.AppendChild(CreateParagraph("10. SYNTHÈSE DSI", true, 14));
                            if (!string.IsNullOrWhiteSpace(fiche.PointsForts))
                            {
                                body.AppendChild(CreateParagraph("Points forts:", true, 11));
                                body.AppendChild(CreateParagraph(fiche.PointsForts, false, 11));
                            }
                            if (!string.IsNullOrWhiteSpace(fiche.PointsVigilance))
                            {
                                body.AppendChild(CreateParagraph("Points de vigilance:", true, 11));
                                body.AppendChild(CreateParagraph(fiche.PointsVigilance, false, 11));
                            }
                            if (!string.IsNullOrWhiteSpace(fiche.DecisionsAttendues))
                            {
                                body.AppendChild(CreateParagraph("Décisions attendues:", true, 11));
                                body.AppendChild(CreateParagraph(fiche.DecisionsAttendues, false, 11));
                            }
                            if (!string.IsNullOrWhiteSpace(fiche.DemandesArbitrage))
                            {
                                body.AppendChild(CreateParagraph("Demandes d'arbitrage:", true, 11));
                                body.AppendChild(CreateParagraph(fiche.DemandesArbitrage, false, 11));
                            }
                        }

                        // Pied de page
                        body.AppendChild(new Paragraph(new Run(new Text(""))));
                        body.AppendChild(CreateParagraph($"Document généré le {DateTime.Now:dd/MM/yyyy à HH:mm}", false, 8, "center"));
                    }

                    return stream.ToArray();
                }
            });
        }
    }
}

