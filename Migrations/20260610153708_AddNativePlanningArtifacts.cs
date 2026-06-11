using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjects.Migrations
{
    /// <inheritdoc />
    public partial class AddNativePlanningArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LignesBudgetPlanificationProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Poste = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Commentaire = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LignesBudgetPlanificationProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LignesBudgetPlanificationProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LignesCommunicationProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Instance = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Objectif = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Frequence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Canal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Participants = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Responsable = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstCopil = table.Column<bool>(type: "bit", nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LignesCommunicationProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LignesCommunicationProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LignesRaciProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeActivite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Activite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Responsable = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Approbateur = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Consulte = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Informe = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LignesRaciProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LignesRaciProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PvKickOffProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateReunion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Heure = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Lieu = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Animateur = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Objectifs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Participants = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrdreDuJour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Decisions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Actions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Commentaires = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PvKickOffProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PvKickOffProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TachesPlanningProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeWbs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Responsable = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Dependances = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Commentaire = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateDebutPrevue = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateFinPrevue = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Avancement = table.Column<int>(type: "int", nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    EstJalon = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TachesPlanningProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TachesPlanningProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LignesBudgetPlanificationProjets_ProjetId",
                table: "LignesBudgetPlanificationProjets",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesCommunicationProjets_ProjetId",
                table: "LignesCommunicationProjets",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesRaciProjets_ProjetId",
                table: "LignesRaciProjets",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_PvKickOffProjets_ProjetId",
                table: "PvKickOffProjets",
                column: "ProjetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TachesPlanningProjets_ProjetId_Ordre",
                table: "TachesPlanningProjets",
                columns: new[] { "ProjetId", "Ordre" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LignesBudgetPlanificationProjets");

            migrationBuilder.DropTable(
                name: "LignesCommunicationProjets");

            migrationBuilder.DropTable(
                name: "LignesRaciProjets");

            migrationBuilder.DropTable(
                name: "PvKickOffProjets");

            migrationBuilder.DropTable(
                name: "TachesPlanningProjets");
        }
    }
}
