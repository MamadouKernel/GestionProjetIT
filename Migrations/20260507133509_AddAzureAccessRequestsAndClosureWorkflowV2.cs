using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjects.Migrations
{
    /// <inheritdoc />
    public partial class AddAzureAccessRequestsAndClosureWorkflowV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommentaireInitiateur",
                table: "DemandesClotureProjets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "DemandesAccesAzureAd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Matricule = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Prenoms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AzureDepartment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DirectionDetecteeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    CommentaireTraitement = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateTraitement = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TraiteParId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UtilisateurCreeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandesAccesAzureAd", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemandesAccesAzureAd_Directions_DirectionDetecteeId",
                        column: x => x.DirectionDetecteeId,
                        principalTable: "Directions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DemandesAccesAzureAd_Utilisateurs_TraiteParId",
                        column: x => x.TraiteParId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DemandesAccesAzureAd_Utilisateurs_UtilisateurCreeId",
                        column: x => x.UtilisateurCreeId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemandesAccesAzureAd_DirectionDetecteeId",
                table: "DemandesAccesAzureAd",
                column: "DirectionDetecteeId");

            migrationBuilder.CreateIndex(
                name: "IX_DemandesAccesAzureAd_Email_Statut_EstSupprime",
                table: "DemandesAccesAzureAd",
                columns: new[] { "Email", "Statut", "EstSupprime" });

            migrationBuilder.CreateIndex(
                name: "IX_DemandesAccesAzureAd_TraiteParId",
                table: "DemandesAccesAzureAd",
                column: "TraiteParId");

            migrationBuilder.CreateIndex(
                name: "IX_DemandesAccesAzureAd_UtilisateurCreeId",
                table: "DemandesAccesAzureAd",
                column: "UtilisateurCreeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemandesAccesAzureAd");

            migrationBuilder.DropColumn(
                name: "CommentaireInitiateur",
                table: "DemandesClotureProjets");
        }
    }
}
