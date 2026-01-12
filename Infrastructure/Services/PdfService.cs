using GestionProjects.Domain.Enums;
using GestionProjects.Domain.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestionProjects.Infrastructure.Services
{
    public interface IPdfService
    {
        Task<byte[]> GenerateCharteProjetPdfAsync(Projet projet);
        Task<byte[]> GenerateCharteProjetCompletPdfAsync(CharteProjet charte);
        Task<byte[]> GenerateFicheProjetPdfAsync(FicheProjet fiche);
        Task<byte[]> GeneratePortefeuilleProjetsPdfAsync(PortefeuilleProjet portefeuille, List<Projet> projets);
        Task<byte[]> GenerateRapportDSIDGPdfAsync(List<Projet> projets);
    }

    public class PdfService : IPdfService
    {
        public PdfService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<byte[]> GenerateCharteProjetPdfAsync(Projet projet)
        {
            return await Task.Run(() =>
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header()
                            .AlignCenter()
                            .Text("CHARTE PROJET")
                            .FontSize(20)
                            .Bold()
                            .FontColor(Colors.Blue.Darken3);

                        page.Content()
                            .PaddingVertical(1, Unit.Centimetre)
                            .Column(column =>
                            {
                                column.Spacing(1, Unit.Centimetre);

                                // Informations générales
                                column.Item().Text("INFORMATIONS GÉNÉRALES").FontSize(14).Bold();
                                column.Item().Text($"Code Projet: {projet.CodeProjet}");
                                column.Item().Text($"Titre: {projet.Titre}");
                                column.Item().Text($"Direction: {projet.Direction?.Libelle ?? "N/A"}");
                                column.Item().Text($"Sponsor: {projet.Sponsor?.Nom} {projet.Sponsor?.Prenoms}");
                                column.Item().Text($"Chef de Projet: {projet.ChefProjet?.Nom} {projet.ChefProjet?.Prenoms ?? "Non assigné"}");
                                column.Item().Text($"Date de début: {(projet.DateDebut.HasValue ? projet.DateDebut.Value.ToString("dd/MM/yyyy") : "N/A")}");
                                column.Item().Text($"Date de fin prévue: {(projet.DateFinPrevue.HasValue ? projet.DateFinPrevue.Value.ToString("dd/MM/yyyy") : "N/A")}");

                                column.Item().PaddingTop(0.5f, Unit.Centimetre);

                                // Contexte et objectifs
                                if (projet.DemandeProjet != null)
                                {
                                    column.Item().Text("CONTEXTE ET OBJECTIFS").FontSize(14).Bold();
                                    column.Item().Text($"Contexte: {projet.DemandeProjet.Contexte ?? "N/A"}");
                                    column.Item().Text($"Objectifs: {projet.DemandeProjet.Objectifs ?? "N/A"}");
                                    column.Item().Text($"Avantages attendus: {projet.DemandeProjet.AvantagesAttendus ?? "N/A"}");
                                }

                                column.Item().PaddingTop(0.5f, Unit.Centimetre);

                                // État du projet
                                column.Item().Text("ÉTAT DU PROJET").FontSize(14).Bold();
                                column.Item().Text($"Statut: {projet.StatutProjet}");
                                column.Item().Text($"Phase actuelle: {projet.PhaseActuelle}");
                                column.Item().Text($"État: {projet.EtatProjet}");
                                column.Item().Text($"Avancement: {projet.PourcentageAvancement}%");

                                column.Item().PaddingTop(0.5f, Unit.Centimetre);

                                // Membres de l'équipe
                                if (projet.Membres != null && projet.Membres.Any())
                                {
                                    column.Item().Text("ÉQUIPE PROJET").FontSize(14).Bold();
                                    foreach (var membre in projet.Membres)
                                    {
                                        column.Item().Text($"- {membre.Nom} {membre.Prenom} ({membre.RoleDansProjet})");
                                    }
                                }

                                column.Item().PaddingTop(0.5f, Unit.Centimetre);

                                // Risques
                                if (projet.Risques != null && projet.Risques.Any())
                                {
                                    column.Item().Text("RISQUES IDENTIFIÉS").FontSize(14).Bold();
                                    foreach (var risque in projet.Risques)
                                    {
                                        column.Item().Text($"- {risque.Description} (Probabilité: {risque.Probabilite}, Impact: {risque.Impact})");
                                    }
                                }
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(text =>
                            {
                                text.Span("Document généré le ").FontSize(8).FontColor(Colors.Grey.Medium);
                                text.Span(DateTime.Now.ToString("dd/MM/yyyy à HH:mm")).Bold().FontSize(8).FontColor(Colors.Grey.Medium);
                            });
                    });
                });

                return document.GeneratePdf();
            });
        }

        public async Task<byte[]> GenerateCharteProjetCompletPdfAsync(CharteProjet charte)
        {
            return await Task.Run(() =>
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header()
                            .PaddingBottom(1, Unit.Centimetre)
                            .Row(row =>
                            {
                                row.RelativeItem().Column(column =>
                                {
                                    column.Item().Text(charte.Departement ?? "").FontSize(10).FontColor(Colors.Grey.Medium);
                                    column.Item().Text(charte.TypeDocument ?? "").FontSize(12).Bold();
                                    column.Item().Text(charte.CodeDocument ?? "").FontSize(9).FontColor(Colors.Grey.Medium);
                                });
                            });

                        page.Content()
                            .PaddingVertical(0.5f, Unit.Centimetre)
                            .Column(column =>
                            {
                                column.Spacing(0.8f, Unit.Centimetre);

                                // Titre
                                column.Item().Text("CHARTE DU PROJET").FontSize(18).Bold().FontColor(Colors.Blue.Darken3);
                                column.Item().Text(charte.NomProjet ?? "").FontSize(14).Bold();

                                // Identification du projet
                                column.Item().Text("IDENTIFICATION DU PROJET").FontSize(12).Bold();
                                column.Item().Text($"Nom du Projet: {charte.NomProjet ?? "N/A"}");
                                column.Item().Text($"Numéro du projet: {charte.NumeroProjet ?? "N/A"}");
                                column.Item().PaddingTop(0.3f, Unit.Centimetre);
                                column.Item().Text("Objectif du projet:").Bold();
                                if (!string.IsNullOrWhiteSpace(charte.ObjectifProjet))
                                {
                                    foreach (var objectif in charte.ObjectifProjet.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text($"• {objectif.Trim()}");
                                    }
                                }

                                // Assurance qualité
                                if (!string.IsNullOrWhiteSpace(charte.AssuranceQualite))
                                {
                                    column.Item().PaddingTop(0.3f, Unit.Centimetre);
                                    column.Item().Text("Assurance de la qualité:").Bold();
                                    foreach (var ligne in charte.AssuranceQualite.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text($"• {ligne.Trim()}");
                                    }
                                }

                                // Périmètre
                                if (!string.IsNullOrWhiteSpace(charte.Perimetre))
                                {
                                    column.Item().PaddingTop(0.3f, Unit.Centimetre);
                                    column.Item().Text("Périmètre:").Bold();
                                    foreach (var ligne in charte.Perimetre.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text($"• {ligne.Trim()}");
                                    }
                                }

                                // Contraintes
                                if (!string.IsNullOrWhiteSpace(charte.ContraintesInitiales))
                                {
                                    column.Item().PaddingTop(0.3f, Unit.Centimetre);
                                    column.Item().Text("Contraintes Initiales:").Bold();
                                    foreach (var ligne in charte.ContraintesInitiales.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text($"• {ligne.Trim()}");
                                    }
                                }

                                // Risques
                                if (!string.IsNullOrWhiteSpace(charte.RisquesInitiaux))
                                {
                                    column.Item().PaddingTop(0.3f, Unit.Centimetre);
                                    column.Item().Text("Risques Initiaux:").Bold();
                                    foreach (var ligne in charte.RisquesInitiaux.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text($"• {ligne.Trim()}");
                                    }
                                }

                                // Acteurs
                                column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                column.Item().Text("ACTEURS DU PROJET").FontSize(12).Bold();
                                column.Item().Text($"Demandeur: {charte.Demandeur?.Nom} {charte.Demandeur?.Prenoms}");
                                column.Item().Text($"Sponsors: {charte.Sponsors ?? "N/A"}");
                                column.Item().Text($"Chef de Projet: {charte.ChefProjet?.Nom} {charte.ChefProjet?.Prenoms}");
                                if (!string.IsNullOrWhiteSpace(charte.EmailChefProjet))
                                {
                                    column.Item().Text($"Email: {charte.EmailChefProjet}");
                                }

                                // Jalons
                                if (charte.Jalons != null && charte.Jalons.Any(j => !j.EstSupprime))
                                {
                                    column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                    column.Item().Text("ÉLÉMENTS DE PLANIFICATION PRÉVISIONNELLE").FontSize(12).Bold();
                                    foreach (var jalon in charte.Jalons.Where(j => !j.EstSupprime).OrderBy(j => j.Ordre))
                                    {
                                        column.Item().Text($"{jalon.Nom}").Bold();
                                        column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text($"Description: {jalon.Description}");
                                        column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text($"Critères d'Approbation: {jalon.CriteresApprobation}");
                                        if (jalon.DatePrevisionnelle.HasValue)
                                        {
                                            column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text($"Date prévisionnelle: {jalon.DatePrevisionnelle.Value:dd/MM/yyyy}");
                                        }
                                        column.Item().PaddingTop(0.2f, Unit.Centimetre);
                                    }
                                }

                                // Parties prenantes
                                if (charte.PartiesPrenantes != null && charte.PartiesPrenantes.Any(p => !p.EstSupprime))
                                {
                                    column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                    column.Item().Text("PARTIES PRENANTES").FontSize(12).Bold();
                                    foreach (var partie in charte.PartiesPrenantes.Where(p => !p.EstSupprime))
                                    {
                                        column.Item().Text($"{partie.Nom} - {partie.Role}");
                                    }
                                }

                                // Autorisation officielle
                                column.Item().PaddingTop(1, Unit.Centimetre);
                                column.Item().Text("AUTORISATION OFFICIELLE").FontSize(12).Bold();
                                column.Item().Text("Nous, les soussignés, approuvons le lancement du projet.").Italic();
                                
                                column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().Text("Signature Sponsor").Bold();
                                        if (charte.SignatureSponsor && charte.DateSignatureSponsor.HasValue)
                                        {
                                            col.Item().Text("✓ Signé le " + charte.DateSignatureSponsor.Value.ToString("dd/MM/yyyy"));
                                        }
                                        else
                                        {
                                            col.Item().PaddingTop(1, Unit.Centimetre).Text("__________________");
                                        }
                                    });
                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().Text("Signature Chef de Projet").Bold();
                                        if (charte.SignatureChefProjet && charte.DateSignatureChefProjet.HasValue)
                                        {
                                            col.Item().Text("✓ Signé le " + charte.DateSignatureChefProjet.Value.ToString("dd/MM/yyyy"));
                                        }
                                        else
                                        {
                                            col.Item().PaddingTop(1, Unit.Centimetre).Text("__________________");
                                        }
                                    });
                                });
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(text =>
                            {
                                text.Span("Document généré le ").FontSize(8).FontColor(Colors.Grey.Medium);
                                text.Span(DateTime.Now.ToString("dd/MM/yyyy à HH:mm")).Bold().FontSize(8).FontColor(Colors.Grey.Medium);
                            });
                    });
                });

                return document.GeneratePdf();
            });
        }

        public async Task<byte[]> GenerateFicheProjetPdfAsync(FicheProjet fiche)
        {
            return await Task.Run(() =>
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header()
                            .AlignCenter()
                            .Text("FICHE PROJET CIT")
                            .FontSize(18)
                            .Bold()
                            .FontColor(Colors.Blue.Darken3);

                        page.Content()
                            .PaddingVertical(1, Unit.Centimetre)
                            .Column(column =>
                            {
                                column.Spacing(0.8f, Unit.Centimetre);

                                // 1. Identification
                                column.Item().Text("1. IDENTIFICATION DU PROJET").FontSize(14).Bold();
                                column.Item().Text($"Code projet: {fiche.Projet?.CodeProjet ?? "N/A"}");
                                column.Item().Text($"Titre court: {fiche.TitreCourt ?? fiche.Projet?.Titre ?? "N/A"}");
                                column.Item().Text($"Titre long: {fiche.TitreLong ?? fiche.Projet?.Titre ?? "N/A"}");
                                column.Item().Text($"Direction: {fiche.Projet?.Direction?.Libelle ?? "N/A"}");
                                column.Item().Text($"Demandeur: {fiche.Projet?.DemandeProjet?.Demandeur?.Nom} {fiche.Projet?.DemandeProjet?.Demandeur?.Prenoms}");
                                column.Item().Text($"Sponsor: {fiche.Projet?.Sponsor?.Nom} {fiche.Projet?.Sponsor?.Prenoms}");
                                column.Item().Text($"Chef de Projet DSI: {fiche.Projet?.ChefProjet?.Nom} {fiche.Projet?.ChefProjet?.Prenoms ?? "Non assigné"}");

                                // 2. Objectifs & Description
                                column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                column.Item().Text("2. OBJECTIFS & DESCRIPTION").FontSize(14).Bold();
                                if (!string.IsNullOrWhiteSpace(fiche.ObjectifPrincipal))
                                {
                                    column.Item().Text("Objectif principal:").Bold();
                                    column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text(fiche.ObjectifPrincipal);
                                }
                                if (!string.IsNullOrWhiteSpace(fiche.ContexteProblemeAdresse))
                                {
                                    column.Item().Text("Contexte / Problème adressé:").Bold();
                                    column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text(fiche.ContexteProblemeAdresse);
                                }
                                if (!string.IsNullOrWhiteSpace(fiche.DescriptionSynthetique))
                                {
                                    column.Item().Text("Description synthétique:").Bold();
                                    column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text(fiche.DescriptionSynthetique);
                                }
                                if (!string.IsNullOrWhiteSpace(fiche.ResultatsAttendus))
                                {
                                    column.Item().Text("Résultats attendus:").Bold();
                                    column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text(fiche.ResultatsAttendus);
                                }

                                // 3. Périmètre
                                if (!string.IsNullOrWhiteSpace(fiche.PerimetreInclus) || !string.IsNullOrWhiteSpace(fiche.PerimetreExclu))
                                {
                                    column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                    column.Item().Text("3. PÉRIMÈTRE").FontSize(14).Bold();
                                    if (!string.IsNullOrWhiteSpace(fiche.PerimetreInclus))
                                    {
                                        column.Item().Text("Périmètre inclus (IN):").Bold();
                                        foreach (var ligne in fiche.PerimetreInclus.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                                        {
                                            column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text($"• {ligne.Trim()}");
                                        }
                                    }
                                    if (!string.IsNullOrWhiteSpace(fiche.PerimetreExclu))
                                    {
                                        column.Item().Text("Périmètre exclu (OUT):").Bold();
                                        foreach (var ligne in fiche.PerimetreExclu.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                                        {
                                            column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text($"• {ligne.Trim()}");
                                        }
                                    }
                                }

                                // 4. Indicateurs clés
                                column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                column.Item().Text("4. INDICATEURS CLÉS").FontSize(14).Bold();
                                if (!string.IsNullOrWhiteSpace(fiche.BeneficesAttendus))
                                {
                                    column.Item().Text("Bénéfices attendus:").Bold();
                                    column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text(fiche.BeneficesAttendus);
                                }
                                column.Item().Text($"Criticité / Urgence: {fiche.CriticiteUrgence ?? "N/A"}");
                                column.Item().Text($"Type de projet: {fiche.TypeProjet ?? "N/A"}");

                                // 5. Planning synthétique
                                column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                column.Item().Text("5. PLANNING SYNTHÉTIQUE").FontSize(14).Bold();
                                column.Item().Text($"Phase actuelle: {fiche.Projet?.PhaseActuelle ?? PhaseProjet.Demande}");
                                column.Item().Text($"Date début: {(fiche.Projet?.DateDebut?.ToString("dd/MM/yyyy") ?? "N/A")}");
                                column.Item().Text($"Date fin prévue: {(fiche.Projet?.DateFinPrevue?.ToString("dd/MM/yyyy") ?? "N/A")}");
                                column.Item().Text($"Prochain jalon: {fiche.ProchainJalon ?? "N/A"}");
                                column.Item().Text($"Statut: {fiche.Projet?.EtatProjet ?? EtatProjet.Vert}");
                                column.Item().Text($"% Avancement: {fiche.Projet?.PourcentageAvancement ?? 0}%");

                                // 6. Principaux risques
                                if (!string.IsNullOrWhiteSpace(fiche.SyntheseRisques))
                                {
                                    column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                    column.Item().Text("6. PRINCIPAUX RISQUES").FontSize(14).Bold();
                                    column.Item().Text(fiche.SyntheseRisques);
                                }

                                // 7. Gouvernance
                                column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                column.Item().Text("7. GOUVERNANCE ET ACTEURS").FontSize(14).Bold();
                                column.Item().Text($"Chef de Projet DSI: {fiche.Projet?.ChefProjet?.Nom} {fiche.Projet?.ChefProjet?.Prenoms ?? "Non assigné"}");
                                column.Item().Text($"Sponsor: {fiche.Projet?.Sponsor?.Nom} {fiche.Projet?.Sponsor?.Prenoms}");
                                if (!string.IsNullOrWhiteSpace(fiche.EquipeProjet))
                                {
                                    column.Item().Text("Équipe projet:").Bold();
                                    foreach (var ligne in fiche.EquipeProjet.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text($"• {ligne.Trim()}");
                                    }
                                }
                                if (!string.IsNullOrWhiteSpace(fiche.PartiesPrenantesCles))
                                {
                                    column.Item().Text("Parties prenantes clés:").Bold();
                                    column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text(fiche.PartiesPrenantesCles);
                                }

                                // 8. Livrables obligatoires
                                column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                column.Item().Text("8. LIVRABLES OBLIGATOIRES").FontSize(14).Bold();
                                column.Item().Text($"Charte Projet: {(fiche.CharteProjetPresente ? "✔" : "✖")}");
                                column.Item().Text($"WBS / Planning / RACI / Budget: {(fiche.WBSPlanningRACIBudgetPresent ? "✔" : "✖")}");
                                column.Item().Text($"CR réunions: {(fiche.CRReunionsPresent ? "✔" : "✖")}");
                                column.Item().Text($"Cahier tests / PV Recette / PV MEP: {(fiche.CahierTestsPVRecettePVMEPPresent ? "✔" : "✖")}");
                                column.Item().Text($"Rapport / Leçons apprises / PV Clôture: {(fiche.RapportLeconsApprisesPVCloturePresent ? "✔" : "✖")}");

                                // 9. Budget
                                if (fiche.BudgetPrevisionnel.HasValue || fiche.BudgetConsomme.HasValue)
                                {
                                    column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                    column.Item().Text("9. BUDGET").FontSize(14).Bold();
                                    column.Item().Text($"Budget prévisionnel: {fiche.BudgetPrevisionnel?.ToString("N2") ?? "N/A"} FCFA");
                                    column.Item().Text($"Budget consommé: {fiche.BudgetConsomme?.ToString("N2") ?? "N/A"} FCFA");
                                    if (fiche.EcartsBudget.HasValue)
                                    {
                                        column.Item().Text($"Écarts: {fiche.EcartsBudget.Value.ToString("N2")} FCFA");
                                    }
                                }

                                // 10. Synthèse DSI
                                if (!string.IsNullOrWhiteSpace(fiche.PointsForts) || !string.IsNullOrWhiteSpace(fiche.PointsVigilance))
                                {
                                    column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                    column.Item().Text("10. SYNTHÈSE DSI").FontSize(14).Bold();
                                    if (!string.IsNullOrWhiteSpace(fiche.PointsForts))
                                    {
                                        column.Item().Text("Points forts:").Bold();
                                        column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text(fiche.PointsForts);
                                    }
                                    if (!string.IsNullOrWhiteSpace(fiche.PointsVigilance))
                                    {
                                        column.Item().Text("Points de vigilance:").Bold();
                                        column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text(fiche.PointsVigilance);
                                    }
                                    if (!string.IsNullOrWhiteSpace(fiche.DecisionsAttendues))
                                    {
                                        column.Item().Text("Décisions attendues:").Bold();
                                        column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text(fiche.DecisionsAttendues);
                                    }
                                    if (!string.IsNullOrWhiteSpace(fiche.DemandesArbitrage))
                                    {
                                        column.Item().Text("Demandes d'arbitrage:").Bold();
                                        column.Item().PaddingLeft(0.5f, Unit.Centimetre).Text(fiche.DemandesArbitrage);
                                    }
                                }
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(text =>
                            {
                                text.Span("Document généré le ").FontSize(8).FontColor(Colors.Grey.Medium);
                                text.Span(DateTime.Now.ToString("dd/MM/yyyy à HH:mm")).Bold().FontSize(8).FontColor(Colors.Grey.Medium);
                            });
                    });
                });

                return document.GeneratePdf();
            });
        }

        public async Task<byte[]> GeneratePortefeuilleProjetsPdfAsync(PortefeuilleProjet portefeuille, List<Projet> projets)
        {
            return await Task.Run(() =>
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header()
                            .Row(row =>
                            {
                                row.RelativeItem().Column(column =>
                                {
                                    column.Item().Text("CÔTE D'IVOIRE TERMINAL").FontSize(12).Bold();
                                    column.Item().Text("ABIDJAN").FontSize(10);
                                });
                                row.RelativeItem().AlignRight().Text(portefeuille.Nom ?? "").FontSize(14).Bold().FontColor(Colors.Blue.Darken3);
                            });
                        
                        page.Content()
                            .PaddingTop(1, Unit.Centimetre)

                            .Column(column =>
                            {
                                column.Spacing(1, Unit.Centimetre);

                                // Objectif Stratégique Global
                                column.Item()
                                    .Background(Colors.Blue.Darken3)
                                    .Padding(1, Unit.Centimetre)
                                    .Column(col =>
                                    {
                                        col.Item().Text("OBJECTIF STRATÉGIQUE GLOBAL").FontSize(14).Bold().FontColor(Colors.White);
                                        col.Item().Text(portefeuille.ObjectifStrategiqueGlobal ?? "");
                                    });

                                // Avantages Attendus
                                if (!string.IsNullOrWhiteSpace(portefeuille.AvantagesAttendus))
                                {
                                    column.Item()
                                        .Background(Colors.Green.Lighten4)
                                        .Padding(1, Unit.Centimetre)
                                        .Column(col =>
                                        {
                                            col.Item().Text("AVANTAGES ATTENDUS DU PORTEFEUILLE").FontSize(12).Bold();
                                            foreach (var avantage in portefeuille.AvantagesAttendus.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                                            {
                                                var ligne = avantage.Trim();
                                                if (ligne.StartsWith("•"))
                                                {
                                                    col.Item().PaddingLeft(0.3f, Unit.Centimetre).Text(ligne);
                                                }
                                                else
                                                {
                                                    col.Item().PaddingLeft(0.3f, Unit.Centimetre).Text($"• {ligne}");
                                                }
                                            }
                                        });
                                }

                                // Risques et Mitigations
                                if (!string.IsNullOrWhiteSpace(portefeuille.RisquesEtMitigations))
                                {
                                    column.Item()
                                        .Background(Colors.Orange.Lighten4)
                                        .Padding(1, Unit.Centimetre)
                                        .Column(col =>
                                        {
                                            col.Item().Text("RISQUES ET MITIGATIONS GLOBAUX").FontSize(12).Bold();
                                            foreach (var ligne in portefeuille.RisquesEtMitigations.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                                            {
                                                var ligneNettoyee = ligne.Trim();
                                                if (!string.IsNullOrWhiteSpace(ligneNettoyee))
                                                {
                                                    var parts = ligneNettoyee.Split(':', 2);
                                                    if (parts.Length == 2)
                                                    {
                                                        col.Item().Text(parts[0].Trim()).Bold();
                                                        col.Item().PaddingLeft(0.5f, Unit.Centimetre).Text($"Mitigation: {parts[1].Trim()}");
                                                    }
                                                    else
                                                    {
                                                        col.Item().Text(ligneNettoyee);
                                                    }
                                                }
                                            }
                                        });
                                }

                                // Projets inclus
                                column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                column.Item().Text("PROJETS INCLUS DANS LE PORTEFEUILLE").FontSize(14).Bold();

                                int index = 1;
                                foreach (var projet in projets)
                                {
                                    column.Item()
                                        .Border(1)
                                        .BorderColor(Colors.Grey.Lighten1)
                                        .Padding(0.5f, Unit.Centimetre)
                                        .Column(col =>
                                        {
                                            col.Item().Text($"{index}. {projet.CodeProjet} - {projet.Titre}").Bold();
                                            if (!string.IsNullOrWhiteSpace(projet.Objectif))
                                            {
                                                col.Item().Text($"Objectif: {projet.Objectif}").FontSize(9);
                                            }
                                            col.Item().Row(row =>
                                            {
                                                row.RelativeItem().Text($"Sponsor: {projet.Sponsor?.Nom} {projet.Sponsor?.Prenoms}").FontSize(9);
                                                row.RelativeItem().Text($"Chef Projet: {projet.ChefProjet?.Nom} {projet.ChefProjet?.Prenoms ?? "Non assigné"}").FontSize(9);
                                            });
                                            col.Item().Row(row =>
                                            {
                                                row.RelativeItem().Text($"Statut: {projet.StatutProjet}").FontSize(9);
                                                row.RelativeItem().Text($"Phase: {projet.PhaseActuelle}").FontSize(9);
                                                row.RelativeItem().Text($"Avancement: {projet.PourcentageAvancement}%").FontSize(9);
                                            });
                                        });

                                    index++;
                                    if (index > projets.Count) break;
                                }
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(text =>
                            {
                                text.Span("Document généré le ").FontSize(8).FontColor(Colors.Grey.Medium);
                                text.Span(DateTime.Now.ToString("dd/MM/yyyy à HH:mm")).Bold().FontSize(8).FontColor(Colors.Grey.Medium);
                            });
                    });
                });

                return document.GeneratePdf();
            });
        }

        public async Task<byte[]> GenerateRapportDSIDGPdfAsync(List<Projet> projets)
        {
            return await Task.Run(() =>
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header()
                            .AlignCenter()
                            .Text("RAPPORT DE GOUVERNANCE DSI/DG")
                            .FontSize(20)
                            .Bold()
                            .FontColor(Colors.Blue.Darken3);

                        page.Footer()
                            .AlignCenter()
                            .Text(text =>
                            {
                                text.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                                text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                                text.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                                text.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                            });

                        page.Content()
                            .PaddingVertical(1, Unit.Centimetre)
                            .Column(column =>
                            {
                                column.Spacing(1, Unit.Centimetre);

                                // En-tête avec date
                                column.Item().Text($"Date du rapport: {DateTime.Now:dd/MM/yyyy HH:mm}")
                                    .FontSize(9)
                                    .FontColor(Colors.Grey.Medium);

                                column.Item().PaddingTop(0.5f, Unit.Centimetre);

                                // Synthèse globale
                                column.Item().Text("SYNTHÈSE GLOBALE").FontSize(14).Bold();
                                column.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Element(CellStyle).Text("Total Projets").Bold();
                                        header.Cell().Element(CellStyle).Text("En Cours").Bold();
                                        header.Cell().Element(CellStyle).Text("Clôturés").Bold();
                                        header.Cell().Element(CellStyle).Text("Suspendus").Bold();
                                    });

                                    var totalProjets = projets.Count;
                                    var enCours = projets.Count(p => p.StatutProjet == StatutProjet.EnCours);
                                    var clotures = projets.Count(p => p.StatutProjet == StatutProjet.Cloture);
                                    var suspendus = projets.Count(p => p.StatutProjet == StatutProjet.Suspendu);

                                    table.Cell().Element(CellStyle).Text(totalProjets.ToString());
                                    table.Cell().Element(CellStyle).Text(enCours.ToString());
                                    table.Cell().Element(CellStyle).Text(clotures.ToString());
                                    table.Cell().Element(CellStyle).Text(suspendus.ToString());
                                });

                                column.Item().PaddingTop(0.5f, Unit.Centimetre);

                                // Indicateurs RAG
                                column.Item().Text("INDICATEURS RAG").FontSize(14).Bold();
                                column.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Element(CellStyle).Text("🟢 Vert").Bold();
                                        header.Cell().Element(CellStyle).Text("🟡 Amber").Bold();
                                        header.Cell().Element(CellStyle).Text("🔴 Rouge").Bold();
                                    });

                                    var vert = projets.Count(p => p.IndicateurRAG == IndicateurRAG.Vert);
                                    var amber = projets.Count(p => p.IndicateurRAG == IndicateurRAG.Amber);
                                    var rouge = projets.Count(p => p.IndicateurRAG == IndicateurRAG.Rouge);

                                    table.Cell().Element(CellStyle).Text(vert.ToString());
                                    table.Cell().Element(CellStyle).Text(amber.ToString());
                                    table.Cell().Element(CellStyle).Text(rouge.ToString());
                                });

                                column.Item().PaddingTop(0.5f, Unit.Centimetre);

                                // Liste des projets avec RAG
                                column.Item().Text("DÉTAIL DES PROJETS").FontSize(14).Bold();
                                column.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1.5f);
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Element(CellStyle).Text("Projet").Bold();
                                        header.Cell().Element(CellStyle).Text("Direction").Bold();
                                        header.Cell().Element(CellStyle).Text("RAG").Bold();
                                        header.Cell().Element(CellStyle).Text("Phase").Bold();
                                        header.Cell().Element(CellStyle).Text("Avancement").Bold();
                                        header.Cell().Element(CellStyle).Text("Statut").Bold();
                                    });

                                    foreach (var projet in projets.OrderBy(p => p.IndicateurRAG).ThenBy(p => p.CodeProjet))
                                    {
                                        var ragLabel = projet.IndicateurRAG switch
                                        {
                                            IndicateurRAG.Vert => "🟢 Vert",
                                            IndicateurRAG.Amber => "🟡 Amber",
                                            IndicateurRAG.Rouge => "🔴 Rouge",
                                            _ => "N/A"
                                        };

                                        table.Cell().Element(CellStyle).Text($"{projet.CodeProjet} - {projet.Titre}");
                                        table.Cell().Element(CellStyle).Text(projet.Direction?.Libelle ?? "N/A");
                                        table.Cell().Element(CellStyle).Text(ragLabel);
                                        table.Cell().Element(CellStyle).Text(projet.PhaseActuelle.ToString());
                                        table.Cell().Element(CellStyle).Text($"{projet.PourcentageAvancement}%");
                                        table.Cell().Element(CellStyle).Text(projet.StatutProjet.ToString());
                                    }
                                });
                                
                                // Section Budget si disponible
                                var projetsAvecBudget = projets.Where(p => p.FicheProjet != null && p.FicheProjet.BudgetPrevisionnel.HasValue).ToList();
                                if (projetsAvecBudget.Any())
                                {
                                    column.Item().PaddingTop(0.5f, Unit.Centimetre);
                                    column.Item().Text("SYNTHÈSE BUDGÉTAIRE").FontSize(14).Bold();
                                    column.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(2);
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(CellStyle).Text("Projet").Bold();
                                            header.Cell().Element(CellStyle).Text("Budget Prév.").Bold();
                                            header.Cell().Element(CellStyle).Text("Budget Cons.").Bold();
                                            header.Cell().Element(CellStyle).Text("Écart").Bold();
                                        });

                                        foreach (var projet in projetsAvecBudget.OrderBy(p => p.CodeProjet))
                                        {
                                            var fiche = projet.FicheProjet;
                                            var ecart = fiche.BudgetConsomme.HasValue && fiche.BudgetPrevisionnel.HasValue
                                                ? fiche.BudgetConsomme.Value - fiche.BudgetPrevisionnel.Value
                                                : 0;
                                            
                                            table.Cell().Element(CellStyle).Text($"{projet.CodeProjet} - {projet.Titre}");
                                            table.Cell().Element(CellStyle).Text(fiche.BudgetPrevisionnel?.ToString("N2") ?? "0");
                                            table.Cell().Element(CellStyle).Text(fiche.BudgetConsomme?.ToString("N2") ?? "0");
                                            table.Cell().Element(CellStyle).Text(ecart.ToString("N2"));
                                        }
                                    });
                                }
                            });
                    });
                });

                return document.GeneratePdf();
            });
        }

        static IContainer CellStyle(IContainer container)
        {
            return container
                .Border(1)
                .Padding(5)
                .Background(Colors.Grey.Lighten3);
        }
    }
}

