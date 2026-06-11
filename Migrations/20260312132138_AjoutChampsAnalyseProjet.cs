using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjects.Migrations
{
    /// <inheritdoc />
    public partial class AjoutChampsAnalyseProjet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DecisionsPrises",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HypothesesProjet",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotesClarification",
                table: "FicheProjets",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecisionsPrises",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "HypothesesProjet",
                table: "FicheProjets");

            migrationBuilder.DropColumn(
                name: "NotesClarification",
                table: "FicheProjets");
        }
    }
}
