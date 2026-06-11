using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjects.Migrations
{
    /// <inheritdoc />
    public partial class AddDetailedUatTestManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CasTestProjetId",
                table: "AnomaliesProjets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CampagnesTestsProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Environnement = table.Column<int>(type: "int", nullable: false),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    DateLancement = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateCloture = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampagnesTestsProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampagnesTestsProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CasTestsProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampagneTestProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Titre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultatAttendu = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priorite = table.Column<int>(type: "int", nullable: false),
                    EstObligatoire = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CasTestsProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CasTestsProjets_CampagnesTestsProjets_CampagneTestProjetId",
                        column: x => x.CampagneTestProjetId,
                        principalTable: "CampagnesTestsProjets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_CasTestsProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionsTestsProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CasTestProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampagneTestProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    Commentaire = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateExecution = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExecuteParId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AnomalieProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionsTestsProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionsTestsProjets_AnomaliesProjets_AnomalieProjetId",
                        column: x => x.AnomalieProjetId,
                        principalTable: "AnomaliesProjets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_ExecutionsTestsProjets_CampagnesTestsProjets_CampagneTestProjetId",
                        column: x => x.CampagneTestProjetId,
                        principalTable: "CampagnesTestsProjets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_ExecutionsTestsProjets_CasTestsProjets_CasTestProjetId",
                        column: x => x.CasTestProjetId,
                        principalTable: "CasTestsProjets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExecutionsTestsProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExecutionsTestsProjets_Utilisateurs_ExecuteParId",
                        column: x => x.ExecuteParId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnomaliesProjets_CasTestProjetId",
                table: "AnomaliesProjets",
                column: "CasTestProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_CampagnesTestsProjets_ProjetId_Nom",
                table: "CampagnesTestsProjets",
                columns: new[] { "ProjetId", "Nom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CasTestsProjets_CampagneTestProjetId",
                table: "CasTestsProjets",
                column: "CampagneTestProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_CasTestsProjets_ProjetId_Reference",
                table: "CasTestsProjets",
                columns: new[] { "ProjetId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionsTestsProjets_AnomalieProjetId",
                table: "ExecutionsTestsProjets",
                column: "AnomalieProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionsTestsProjets_CampagneTestProjetId",
                table: "ExecutionsTestsProjets",
                column: "CampagneTestProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionsTestsProjets_CasTestProjetId",
                table: "ExecutionsTestsProjets",
                column: "CasTestProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionsTestsProjets_ExecuteParId",
                table: "ExecutionsTestsProjets",
                column: "ExecuteParId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionsTestsProjets_ProjetId",
                table: "ExecutionsTestsProjets",
                column: "ProjetId");

            migrationBuilder.AddForeignKey(
                name: "FK_AnomaliesProjets_CasTestsProjets_CasTestProjetId",
                table: "AnomaliesProjets",
                column: "CasTestProjetId",
                principalTable: "CasTestsProjets",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnomaliesProjets_CasTestsProjets_CasTestProjetId",
                table: "AnomaliesProjets");

            migrationBuilder.DropTable(
                name: "ExecutionsTestsProjets");

            migrationBuilder.DropTable(
                name: "CasTestsProjets");

            migrationBuilder.DropTable(
                name: "CampagnesTestsProjets");

            migrationBuilder.DropIndex(
                name: "IX_AnomaliesProjets_CasTestProjetId",
                table: "AnomaliesProjets");

            migrationBuilder.DropColumn(
                name: "CasTestProjetId",
                table: "AnomaliesProjets");
        }
    }
}
