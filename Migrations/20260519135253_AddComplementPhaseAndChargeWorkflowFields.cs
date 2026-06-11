using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjects.Migrations
{
    /// <inheritdoc />
    public partial class AddComplementPhaseAndChargeWorkflowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChargesProjets_Projets_ProjetId",
                table: "ChargesProjets");

            migrationBuilder.DropForeignKey(
                name: "FK_ChargesProjets_Projets_ProjetId1",
                table: "ChargesProjets");

            migrationBuilder.DropForeignKey(
                name: "FK_ChargesProjets_Utilisateurs_SaisieParId",
                table: "ChargesProjets");

            migrationBuilder.RenameColumn(
                name: "ProjetId1",
                table: "ChargesProjets",
                newName: "ValideeParId");

            migrationBuilder.RenameIndex(
                name: "IX_ChargesProjets_ProjetId1",
                table: "ChargesProjets",
                newName: "IX_ChargesProjets_ValideeParId");

            migrationBuilder.AlterColumn<decimal>(
                name: "CapaciteHebdomadaire",
                table: "Utilisateurs",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "Responsable",
                table: "RisquesProjets",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "PlanMitigation",
                table: "RisquesProjets",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "ActionsAVenirExecution",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActionsRealiseesExecution",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanalCommunication",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChangeRequis",
                table: "FicheProjets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CommentaireAvancementExecution",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommentaireBudgetPlanification",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommentaireStatutFinal",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommentaireValidationPlanification",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CopilPrevu",
                table: "FicheProjets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateDebutRecette",
                table: "FicheProjets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateDebutReelleExecution",
                table: "FicheProjets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateFinEstimeeExecution",
                table: "FicheProjets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateFinRecette",
                table: "FicheProjets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateMepPrevue",
                table: "FicheProjets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionsExecution",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecoupageLotsTravail",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrequenceReunions",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HypercareTermine",
                table: "FicheProjets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IncidentsMep",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IncidentsPostMep",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JalonsPrincipaux",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JustificationBudgetExecution",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JustificationRetardExecution",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParticipantsReunions",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerimetreTeste",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PeriodeHypercare",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanMep",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanRollback",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanificationRessources",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrerequisMep",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProblemesBlocagesExecution",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RaciParActivite",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceChange",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultatMep",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatutFinalCloture",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatutHypercare",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatutValidationChange",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SyntheseChargesExecution",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TransfertRunAcces",
                table: "FicheProjets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TransfertRunDocumentation",
                table: "FicheProjets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TransfertRunExploitationPrete",
                table: "FicheProjets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TransfertRunSupportInforme",
                table: "FicheProjets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UtilisateursTesteurs",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ChargeReelle",
                table: "ChargesProjets",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ChargePrevisionnelle",
                table: "ChargesProjets",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<string>(
                name: "Activite",
                table: "ChargesProjets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CommentaireValidation",
                table: "ChargesProjets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DateSoumissionValidation",
                table: "ChargesProjets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateValidation",
                table: "ChargesProjets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatutValidation",
                table: "ChargesProjets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TypeActivite",
                table: "ChargesProjets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_ChargesProjets_Projets_ProjetId",
                table: "ChargesProjets",
                column: "ProjetId",
                principalTable: "Projets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChargesProjets_Utilisateurs_SaisieParId",
                table: "ChargesProjets",
                column: "SaisieParId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChargesProjets_Utilisateurs_ValideeParId",
                table: "ChargesProjets",
                column: "ValideeParId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChargesProjets_Projets_ProjetId",
                table: "ChargesProjets");

            migrationBuilder.DropForeignKey(
                name: "FK_ChargesProjets_Utilisateurs_SaisieParId",
                table: "ChargesProjets");

            migrationBuilder.DropForeignKey(
                name: "FK_ChargesProjets_Utilisateurs_ValideeParId",
                table: "ChargesProjets");

            migrationBuilder.DropColumn(
                name: "ActionsAVenirExecution",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "ActionsRealiseesExecution",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "CanalCommunication",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "ChangeRequis",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "CommentaireAvancementExecution",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "CommentaireBudgetPlanification",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "CommentaireStatutFinal",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "CommentaireValidationPlanification",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "CopilPrevu",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "DateDebutRecette",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "DateDebutReelleExecution",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "DateFinEstimeeExecution",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "DateFinRecette",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "DateMepPrevue",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "DecisionsExecution",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "DecoupageLotsTravail",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "FrequenceReunions",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "HypercareTermine",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "IncidentsMep",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "IncidentsPostMep",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "JalonsPrincipaux",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "JustificationBudgetExecution",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "JustificationRetardExecution",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "ParticipantsReunions",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "PerimetreTeste",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "PeriodeHypercare",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "PlanMep",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "PlanRollback",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "PlanificationRessources",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "PrerequisMep",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "ProblemesBlocagesExecution",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "RaciParActivite",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "ReferenceChange",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "ResultatMep",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "StatutFinalCloture",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "StatutHypercare",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "StatutValidationChange",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "SyntheseChargesExecution",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "TransfertRunAcces",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "TransfertRunDocumentation",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "TransfertRunExploitationPrete",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "TransfertRunSupportInforme",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "UtilisateursTesteurs",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "Activite",
                table: "ChargesProjets");

            migrationBuilder.DropColumn(
                name: "CommentaireValidation",
                table: "ChargesProjets");

            migrationBuilder.DropColumn(
                name: "DateSoumissionValidation",
                table: "ChargesProjets");

            migrationBuilder.DropColumn(
                name: "DateValidation",
                table: "ChargesProjets");

            migrationBuilder.DropColumn(
                name: "StatutValidation",
                table: "ChargesProjets");

            migrationBuilder.DropColumn(
                name: "TypeActivite",
                table: "ChargesProjets");

            migrationBuilder.RenameColumn(
                name: "ValideeParId",
                table: "ChargesProjets",
                newName: "ProjetId1");

            migrationBuilder.RenameIndex(
                name: "IX_ChargesProjets_ValideeParId",
                table: "ChargesProjets",
                newName: "IX_ChargesProjets_ProjetId1");

            migrationBuilder.AlterColumn<decimal>(
                name: "CapaciteHebdomadaire",
                table: "Utilisateurs",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Responsable",
                table: "RisquesProjets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PlanMitigation",
                table: "RisquesProjets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ChargeReelle",
                table: "ChargesProjets",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ChargePrevisionnelle",
                table: "ChargesProjets",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AddForeignKey(
                name: "FK_ChargesProjets_Projets_ProjetId",
                table: "ChargesProjets",
                column: "ProjetId",
                principalTable: "Projets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChargesProjets_Projets_ProjetId1",
                table: "ChargesProjets",
                column: "ProjetId1",
                principalTable: "Projets",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChargesProjets_Utilisateurs_SaisieParId",
                table: "ChargesProjets",
                column: "SaisieParId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
