using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjects.Migrations
{
    /// <inheritdoc />
    public partial class AddCollaborationAndElectronicSignatureModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollaborationsProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    NomEquipeTeams = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeamId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TeamUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NomCanalTeams = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChannelId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChannelUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NomPlanPlanner = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlanId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlanUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NomBucketPlanner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BucketId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateProvisioning = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DerniereSynchronisationEquipe = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NombreMembresSynchronises = table.Column<int>(type: "int", nullable: false),
                    MessageStatut = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationsProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollaborationsProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DossiersSignatureProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TypeDocument = table.Column<int>(type: "int", nullable: false),
                    Fournisseur = table.Column<int>(type: "int", nullable: false),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    LivrableSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NomDocumentSource = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CheminDocumentSource = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NomDocumentSigne = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CheminDocumentSigne = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExternalRequestId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UrlSuivi = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MessageStatut = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateEnvoi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateFinalisation = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateExpiration = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DossiersSignatureProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DossiersSignatureProjets_LivrablesProjets_LivrableSourceId",
                        column: x => x.LivrableSourceId,
                        principalTable: "LivrablesProjets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_DossiersSignatureProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TachesCollaborationProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollaborationProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: false),
                    Titre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    DateEcheance = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssigneeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalTaskId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExternalBucketId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSynchronisee = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TachesCollaborationProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TachesCollaborationProjets_CollaborationsProjets_CollaborationProjetId",
                        column: x => x.CollaborationProjetId,
                        principalTable: "CollaborationsProjets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_TachesCollaborationProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TachesCollaborationProjets_Utilisateurs_AssigneeId",
                        column: x => x.AssigneeId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SignatairesDossiersSignatureProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DossierSignatureProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UtilisateurId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NomComplet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    OrdreSignature = table.Column<int>(type: "int", nullable: false),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    DateSignature = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatairesDossiersSignatureProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignatairesDossiersSignatureProjets_DossiersSignatureProjets_DossierSignatureProjetId",
                        column: x => x.DossierSignatureProjetId,
                        principalTable: "DossiersSignatureProjets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SignatairesDossiersSignatureProjets_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationsProjets_ProjetId",
                table: "CollaborationsProjets",
                column: "ProjetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DossiersSignatureProjets_LivrableSourceId",
                table: "DossiersSignatureProjets",
                column: "LivrableSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_DossiersSignatureProjets_ProjetId_TypeDocument",
                table: "DossiersSignatureProjets",
                columns: new[] { "ProjetId", "TypeDocument" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignatairesDossiersSignatureProjets_DossierSignatureProjetId_Role",
                table: "SignatairesDossiersSignatureProjets",
                columns: new[] { "DossierSignatureProjetId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignatairesDossiersSignatureProjets_UtilisateurId",
                table: "SignatairesDossiersSignatureProjets",
                column: "UtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_TachesCollaborationProjets_AssigneeId",
                table: "TachesCollaborationProjets",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_TachesCollaborationProjets_CollaborationProjetId_Phase",
                table: "TachesCollaborationProjets",
                columns: new[] { "CollaborationProjetId", "Phase" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TachesCollaborationProjets_ProjetId",
                table: "TachesCollaborationProjets",
                column: "ProjetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignatairesDossiersSignatureProjets");

            migrationBuilder.DropTable(
                name: "TachesCollaborationProjets");

            migrationBuilder.DropTable(
                name: "DossiersSignatureProjets");

            migrationBuilder.DropTable(
                name: "CollaborationsProjets");
        }
    }
}
