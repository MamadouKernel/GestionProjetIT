using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjects.Migrations
{
    /// <inheritdoc />
    public partial class AddDemandeCodirSponsorSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EstMembreCodir",
                table: "Utilisateurs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "AutreSponsorId",
                table: "DemandesProjets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemandesProjets_AutreSponsorId",
                table: "DemandesProjets",
                column: "AutreSponsorId");

            migrationBuilder.AddForeignKey(
                name: "FK_DemandesProjets_Utilisateurs_AutreSponsorId",
                table: "DemandesProjets",
                column: "AutreSponsorId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DemandesProjets_Utilisateurs_AutreSponsorId",
                table: "DemandesProjets");

            migrationBuilder.DropIndex(
                name: "IX_DemandesProjets_AutreSponsorId",
                table: "DemandesProjets");

            migrationBuilder.DropColumn(
                name: "EstMembreCodir",
                table: "Utilisateurs");

            migrationBuilder.DropColumn(
                name: "AutreSponsorId",
                table: "DemandesProjets");
        }
    }
}
