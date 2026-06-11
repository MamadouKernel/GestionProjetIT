using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjects.Migrations
{
    /// <inheritdoc />
    public partial class AddDemandeCreationCompte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemandesCreationCompte",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Prenoms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Service = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DirectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DirecteurMetierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    CommentaireDM = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommentaireDSI = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateSoumission = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UtilisateurCreePar = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandesCreationCompte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemandesCreationCompte_Directions_DirectionId",
                        column: x => x.DirectionId,
                        principalTable: "Directions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DemandesCreationCompte_Utilisateurs_DirecteurMetierId",
                        column: x => x.DirecteurMetierId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemandesCreationCompte_DirecteurMetierId",
                table: "DemandesCreationCompte",
                column: "DirecteurMetierId");

            migrationBuilder.CreateIndex(
                name: "IX_DemandesCreationCompte_DirectionId",
                table: "DemandesCreationCompte",
                column: "DirectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemandesCreationCompte");
        }
    }
}
