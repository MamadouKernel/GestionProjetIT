using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjects.Migrations
{
    /// <inheritdoc />
    public partial class AddCapaciteRessourcesAndBudgetJustification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CapaciteHebdomadaire",
                table: "Utilisateurs",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateJustificationEcart",
                table: "FicheProjets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JustificationEcartBudget",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JustificationParId",
                table: "FicheProjets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FicheProjets_JustificationParId",
                table: "FicheProjets",
                column: "JustificationParId");

            migrationBuilder.AddForeignKey(
                name: "FK_FicheProjets_Utilisateurs_JustificationParId",
                table: "FicheProjets",
                column: "JustificationParId",
                principalTable: "Utilisateurs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FicheProjets_Utilisateurs_JustificationParId",
                table: "FicheProjets");

            migrationBuilder.DropIndex(
                name: "IX_FicheProjets_JustificationParId",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "CapaciteHebdomadaire",
                table: "Utilisateurs");

            migrationBuilder.DropColumn(
                name: "DateJustificationEcart",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "JustificationEcartBudget",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "JustificationParId",
                table: "FicheProjets");
        }
    }
}
