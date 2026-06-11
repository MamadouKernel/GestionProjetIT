using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjects.Migrations
{
    /// <inheritdoc />
    public partial class initGestProjet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParametresSysteme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Valeur = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametresSysteme", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortefeuillesProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ObjectifStrategiqueGlobal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AvantagesAttendus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RisquesEtMitigations = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortefeuillesProjets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnomaliesProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priorite = table.Column<int>(type: "int", nullable: false),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    Environnement = table.Column<int>(type: "int", nullable: false),
                    ModuleConcerne = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RapporteePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateCreationAnomalie = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssigneeA = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateResolution = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CommentaireResolution = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnomaliesProjets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateAction = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UtilisateurId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TypeAction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Entite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntiteId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnciennesValeurs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NouvellesValeurs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Commentaire = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdresseIP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CharteProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomProjet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NumeroProjet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ObjectifProjet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssuranceQualite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Perimetre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContraintesInitiales = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RisquesInitiaux = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DemandeurId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sponsors = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChefProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailChefProjet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CodeDocument = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TypeDocument = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Departement = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NumeroRevision = table.Column<int>(type: "int", nullable: false),
                    DateRevision = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DescriptionRevision = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RedigePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprouvePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignatureSponsor = table.Column<bool>(type: "bit", nullable: false),
                    DateSignatureSponsor = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignatureSponsorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SignatureChefProjet = table.Column<bool>(type: "bit", nullable: false),
                    DateSignatureChefProjet = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignatureChefProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharteProjets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JalonsCharte",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CharteProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriteresApprobation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DatePrevisionnelle = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JalonsCharte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JalonsCharte_CharteProjets_CharteProjetId",
                        column: x => x.CharteProjetId,
                        principalTable: "CharteProjets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DelegationsChefProjet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DelegantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DelegueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateDebut = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstActive = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationsChefProjet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DelegationsValidationDSI",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DSIId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DelegueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateDebut = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateFin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstActive = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationsValidationDSI", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DemandesClotureProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateDemande = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateSouhaiteeCloture = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DemandeParId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatutValidationDemandeur = table.Column<int>(type: "int", nullable: false),
                    DateValidationDemandeur = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CommentaireDemandeur = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatutValidationDirecteurMetier = table.Column<int>(type: "int", nullable: false),
                    DateValidationDirecteurMetier = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CommentaireDirecteurMetier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatutValidationDSI = table.Column<int>(type: "int", nullable: false),
                    DateValidationDSI = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CommentaireDSI = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstTerminee = table.Column<bool>(type: "bit", nullable: false),
                    DateClotureFinale = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandesClotureProjets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DemandesProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Titre = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Contexte = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Objectifs = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvantagesAttendus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Perimetre = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Urgence = table.Column<int>(type: "int", nullable: false),
                    Criticite = table.Column<int>(type: "int", nullable: false),
                    DateMiseEnOeuvreSouhaitee = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DemandeurId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DirecteurMetierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatutDemande = table.Column<int>(type: "int", nullable: false),
                    DateSoumission = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateValidationDM = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateValidationDSI = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CommentaireDirecteurMetier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommentaireDSI = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CahierChargesPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandesProjets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Directions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstActive = table.Column<bool>(type: "bit", nullable: false),
                    DSIId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Directions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstActive = table.Column<bool>(type: "bit", nullable: false),
                    DirectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Services_Directions_DirectionId",
                        column: x => x.DirectionId,
                        principalTable: "Directions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Utilisateurs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Matricule = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MotDePasse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Prenoms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DirectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateDerniereConnexion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NombreConnexion = table.Column<int>(type: "int", nullable: false),
                    PeutCreerDemandeProjet = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utilisateurs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Utilisateurs_Directions_DirectionId",
                        column: x => x.DirectionId,
                        principalTable: "Directions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentsJointsDemandes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DemandeProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomFichier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CheminRelatif = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateDepot = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeposeParId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentsJointsDemandes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentsJointsDemandes_DemandesProjets_DemandeProjetId",
                        column: x => x.DemandeProjetId,
                        principalTable: "DemandesProjets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentsJointsDemandes_Utilisateurs_DeposeParId",
                        column: x => x.DeposeParId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UtilisateurId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TypeNotification = table.Column<int>(type: "int", nullable: false),
                    Titre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntiteType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EstLue = table.Column<bool>(type: "bit", nullable: false),
                    DateLecture = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DonneesSupplementaires = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PartiesPrenantesCharte",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CharteProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UtilisateurId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartiesPrenantesCharte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartiesPrenantesCharte_CharteProjets_CharteProjetId",
                        column: x => x.CharteProjetId,
                        principalTable: "CharteProjets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartiesPrenantesCharte_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Projets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeProjet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Titre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Objectif = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortefeuilleProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DemandeProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SponsorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChefProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StatutProjet = table.Column<int>(type: "int", nullable: false),
                    PhaseActuelle = table.Column<int>(type: "int", nullable: false),
                    PourcentageAvancement = table.Column<int>(type: "int", nullable: false),
                    EtatProjet = table.Column<int>(type: "int", nullable: false),
                    DateDebut = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateFinPrevue = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateFinReelle = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BilanCloture = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeconsApprises = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecetteValidee = table.Column<bool>(type: "bit", nullable: false),
                    DateRecetteValidee = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecetteValideeParId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MepEffectuee = table.Column<bool>(type: "bit", nullable: false),
                    DateMep = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CharteValidee = table.Column<bool>(type: "bit", nullable: false),
                    DateCharteValidee = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CharteValideeParDM = table.Column<bool>(type: "bit", nullable: false),
                    DateCharteValideeParDM = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CharteValideeParDMId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CharteValideeParDSI = table.Column<bool>(type: "bit", nullable: false),
                    DateCharteValideeParDSI = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CharteValideeParDSIId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CommentaireRefusCharteDM = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommentaireRefusCharteDSI = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlanningValideParDSI = table.Column<bool>(type: "bit", nullable: false),
                    DatePlanningValideParDSI = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlanningValideParDM = table.Column<bool>(type: "bit", nullable: false),
                    DatePlanningValideParDM = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CommentaireTechnique = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateDernierCommentaireTechnique = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DernierCommentaireTechniqueParId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projets_DemandesProjets_DemandeProjetId",
                        column: x => x.DemandeProjetId,
                        principalTable: "DemandesProjets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Projets_Directions_DirectionId",
                        column: x => x.DirectionId,
                        principalTable: "Directions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projets_PortefeuillesProjets_PortefeuilleProjetId",
                        column: x => x.PortefeuilleProjetId,
                        principalTable: "PortefeuillesProjets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Projets_Utilisateurs_CharteValideeParDMId",
                        column: x => x.CharteValideeParDMId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projets_Utilisateurs_CharteValideeParDSIId",
                        column: x => x.CharteValideeParDSIId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projets_Utilisateurs_ChefProjetId",
                        column: x => x.ChefProjetId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projets_Utilisateurs_DernierCommentaireTechniqueParId",
                        column: x => x.DernierCommentaireTechniqueParId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Projets_Utilisateurs_RecetteValideeParId",
                        column: x => x.RecetteValideeParId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projets_Utilisateurs_SponsorId",
                        column: x => x.SponsorId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UtilisateurRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UtilisateurId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    DateDebut = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Commentaire = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilisateurRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UtilisateurRoles_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FicheProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitreCourt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TitreLong = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ObjectifPrincipal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContexteProblemeAdresse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionSynthetique = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultatsAttendus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PerimetreInclus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PerimetreExclu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BeneficesAttendus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CriticiteUrgence = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TypeProjet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProchainJalon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SyntheseRisques = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EquipeProjet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PartiesPrenantesCles = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CharteProjetPresente = table.Column<bool>(type: "bit", nullable: false),
                    WBSPlanningRACIBudgetPresent = table.Column<bool>(type: "bit", nullable: false),
                    CRReunionsPresent = table.Column<bool>(type: "bit", nullable: false),
                    CahierTestsPVRecettePVMEPPresent = table.Column<bool>(type: "bit", nullable: false),
                    RapportLeconsApprisesPVCloturePresent = table.Column<bool>(type: "bit", nullable: false),
                    BudgetPrevisionnel = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BudgetConsomme = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EcartsBudget = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PointsForts = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PointsVigilance = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecisionsAttendues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DemandesArbitrage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateDerniereMiseAJour = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DerniereMiseAJourParId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FicheProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FicheProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FicheProjets_Utilisateurs_DerniereMiseAJourParId",
                        column: x => x.DerniereMiseAJourParId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "HistoriqueChefProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChefProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateDebut = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Commentaire = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoriqueChefProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoriqueChefProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoriqueChefProjets_Utilisateurs_ChefProjetId",
                        column: x => x.ChefProjetId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistoriquePhasesProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: false),
                    StatutProjet = table.Column<int>(type: "int", nullable: false),
                    DateDebut = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifieParId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Commentaire = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoriquePhasesProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoriquePhasesProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistoriquePhasesProjets_Utilisateurs_ModifieParId",
                        column: x => x.ModifieParId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LivrablesProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: false),
                    TypeLivrable = table.Column<int>(type: "int", nullable: false),
                    NomDocument = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CheminRelatif = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateDepot = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeposeParId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Commentaire = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LivrablesProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LivrablesProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LivrablesProjets_Utilisateurs_DeposeParId",
                        column: x => x.DeposeParId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MembresProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Prenom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Fonction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DirectionLibelle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoleDansProjet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembresProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembresProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RisquesProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Probabilite = table.Column<int>(type: "int", nullable: false),
                    Impact = table.Column<int>(type: "int", nullable: false),
                    PlanMitigation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Responsable = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    DateCreationRisque = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateCloture = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RisquesProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RisquesProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnomaliesProjets_ProjetId",
                table: "AnomaliesProjets",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UtilisateurId",
                table: "AuditLogs",
                column: "UtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_CharteProjets_ChefProjetId",
                table: "CharteProjets",
                column: "ChefProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_CharteProjets_DemandeurId",
                table: "CharteProjets",
                column: "DemandeurId");

            migrationBuilder.CreateIndex(
                name: "IX_CharteProjets_ProjetId",
                table: "CharteProjets",
                column: "ProjetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharteProjets_SignatureChefProjetId",
                table: "CharteProjets",
                column: "SignatureChefProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_CharteProjets_SignatureSponsorId",
                table: "CharteProjets",
                column: "SignatureSponsorId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationsChefProjet_DelegantId",
                table: "DelegationsChefProjet",
                column: "DelegantId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationsChefProjet_DelegueId",
                table: "DelegationsChefProjet",
                column: "DelegueId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationsChefProjet_ProjetId",
                table: "DelegationsChefProjet",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationsValidationDSI_DelegueId",
                table: "DelegationsValidationDSI",
                column: "DelegueId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationsValidationDSI_DSIId",
                table: "DelegationsValidationDSI",
                column: "DSIId");

            migrationBuilder.CreateIndex(
                name: "IX_DemandesClotureProjets_DemandeParId",
                table: "DemandesClotureProjets",
                column: "DemandeParId");

            migrationBuilder.CreateIndex(
                name: "IX_DemandesClotureProjets_ProjetId",
                table: "DemandesClotureProjets",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_DemandesProjets_DemandeurId",
                table: "DemandesProjets",
                column: "DemandeurId");

            migrationBuilder.CreateIndex(
                name: "IX_DemandesProjets_DirecteurMetierId",
                table: "DemandesProjets",
                column: "DirecteurMetierId");

            migrationBuilder.CreateIndex(
                name: "IX_DemandesProjets_DirectionId",
                table: "DemandesProjets",
                column: "DirectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Directions_DSIId",
                table: "Directions",
                column: "DSIId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentsJointsDemandes_DemandeProjetId",
                table: "DocumentsJointsDemandes",
                column: "DemandeProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentsJointsDemandes_DeposeParId",
                table: "DocumentsJointsDemandes",
                column: "DeposeParId");

            migrationBuilder.CreateIndex(
                name: "IX_FicheProjets_DerniereMiseAJourParId",
                table: "FicheProjets",
                column: "DerniereMiseAJourParId");

            migrationBuilder.CreateIndex(
                name: "IX_FicheProjets_ProjetId",
                table: "FicheProjets",
                column: "ProjetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoriqueChefProjets_ChefProjetId",
                table: "HistoriqueChefProjets",
                column: "ChefProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoriqueChefProjets_ProjetId",
                table: "HistoriqueChefProjets",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoriquePhasesProjets_ModifieParId",
                table: "HistoriquePhasesProjets",
                column: "ModifieParId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoriquePhasesProjets_ProjetId",
                table: "HistoriquePhasesProjets",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_JalonsCharte_CharteProjetId",
                table: "JalonsCharte",
                column: "CharteProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_LivrablesProjets_DeposeParId",
                table: "LivrablesProjets",
                column: "DeposeParId");

            migrationBuilder.CreateIndex(
                name: "IX_LivrablesProjets_ProjetId",
                table: "LivrablesProjets",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_MembresProjets_ProjetId",
                table: "MembresProjets",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UtilisateurId_EstLue_EstSupprime",
                table: "Notifications",
                columns: new[] { "UtilisateurId", "EstLue", "EstSupprime" });

            migrationBuilder.CreateIndex(
                name: "IX_PartiesPrenantesCharte_CharteProjetId",
                table: "PartiesPrenantesCharte",
                column: "CharteProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_PartiesPrenantesCharte_UtilisateurId",
                table: "PartiesPrenantesCharte",
                column: "UtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_Projets_CharteValideeParDMId",
                table: "Projets",
                column: "CharteValideeParDMId");

            migrationBuilder.CreateIndex(
                name: "IX_Projets_CharteValideeParDSIId",
                table: "Projets",
                column: "CharteValideeParDSIId");

            migrationBuilder.CreateIndex(
                name: "IX_Projets_ChefProjetId",
                table: "Projets",
                column: "ChefProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_Projets_DemandeProjetId",
                table: "Projets",
                column: "DemandeProjetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projets_DernierCommentaireTechniqueParId",
                table: "Projets",
                column: "DernierCommentaireTechniqueParId");

            migrationBuilder.CreateIndex(
                name: "IX_Projets_DirectionId",
                table: "Projets",
                column: "DirectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Projets_PortefeuilleProjetId",
                table: "Projets",
                column: "PortefeuilleProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_Projets_RecetteValideeParId",
                table: "Projets",
                column: "RecetteValideeParId");

            migrationBuilder.CreateIndex(
                name: "IX_Projets_SponsorId",
                table: "Projets",
                column: "SponsorId");

            migrationBuilder.CreateIndex(
                name: "IX_RisquesProjets_ProjetId",
                table: "RisquesProjets",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_DirectionId",
                table: "Services",
                column: "DirectionId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilisateurRoles_UtilisateurId_Role",
                table: "UtilisateurRoles",
                columns: new[] { "UtilisateurId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Utilisateurs_DirectionId",
                table: "Utilisateurs",
                column: "DirectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_AnomaliesProjets_Projets_ProjetId",
                table: "AnomaliesProjets",
                column: "ProjetId",
                principalTable: "Projets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Utilisateurs_UtilisateurId",
                table: "AuditLogs",
                column: "UtilisateurId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CharteProjets_Projets_ProjetId",
                table: "CharteProjets",
                column: "ProjetId",
                principalTable: "Projets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharteProjets_Utilisateurs_ChefProjetId",
                table: "CharteProjets",
                column: "ChefProjetId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CharteProjets_Utilisateurs_DemandeurId",
                table: "CharteProjets",
                column: "DemandeurId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CharteProjets_Utilisateurs_SignatureChefProjetId",
                table: "CharteProjets",
                column: "SignatureChefProjetId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CharteProjets_Utilisateurs_SignatureSponsorId",
                table: "CharteProjets",
                column: "SignatureSponsorId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DelegationsChefProjet_Projets_ProjetId",
                table: "DelegationsChefProjet",
                column: "ProjetId",
                principalTable: "Projets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DelegationsChefProjet_Utilisateurs_DelegantId",
                table: "DelegationsChefProjet",
                column: "DelegantId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DelegationsChefProjet_Utilisateurs_DelegueId",
                table: "DelegationsChefProjet",
                column: "DelegueId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DelegationsValidationDSI_Utilisateurs_DSIId",
                table: "DelegationsValidationDSI",
                column: "DSIId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DelegationsValidationDSI_Utilisateurs_DelegueId",
                table: "DelegationsValidationDSI",
                column: "DelegueId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DemandesClotureProjets_Projets_ProjetId",
                table: "DemandesClotureProjets",
                column: "ProjetId",
                principalTable: "Projets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DemandesClotureProjets_Utilisateurs_DemandeParId",
                table: "DemandesClotureProjets",
                column: "DemandeParId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DemandesProjets_Directions_DirectionId",
                table: "DemandesProjets",
                column: "DirectionId",
                principalTable: "Directions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DemandesProjets_Utilisateurs_DemandeurId",
                table: "DemandesProjets",
                column: "DemandeurId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DemandesProjets_Utilisateurs_DirecteurMetierId",
                table: "DemandesProjets",
                column: "DirecteurMetierId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Directions_Utilisateurs_DSIId",
                table: "Directions",
                column: "DSIId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Directions_Utilisateurs_DSIId",
                table: "Directions");

            migrationBuilder.DropTable(
                name: "AnomaliesProjets");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "DelegationsChefProjet");

            migrationBuilder.DropTable(
                name: "DelegationsValidationDSI");

            migrationBuilder.DropTable(
                name: "DemandesClotureProjets");

            migrationBuilder.DropTable(
                name: "DocumentsJointsDemandes");

            migrationBuilder.DropTable(
                name: "FicheProjets");

            migrationBuilder.DropTable(
                name: "HistoriqueChefProjets");

            migrationBuilder.DropTable(
                name: "HistoriquePhasesProjets");

            migrationBuilder.DropTable(
                name: "JalonsCharte");

            migrationBuilder.DropTable(
                name: "LivrablesProjets");

            migrationBuilder.DropTable(
                name: "MembresProjets");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "ParametresSysteme");

            migrationBuilder.DropTable(
                name: "PartiesPrenantesCharte");

            migrationBuilder.DropTable(
                name: "RisquesProjets");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "UtilisateurRoles");

            migrationBuilder.DropTable(
                name: "CharteProjets");

            migrationBuilder.DropTable(
                name: "Projets");

            migrationBuilder.DropTable(
                name: "DemandesProjets");

            migrationBuilder.DropTable(
                name: "PortefeuillesProjets");

            migrationBuilder.DropTable(
                name: "Utilisateurs");

            migrationBuilder.DropTable(
                name: "Directions");
        }
    }
}
