using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjects.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredBilanAndProfilRessource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProfilRessource",
                table: "Utilisateurs",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LeconsApprises",
                table: "Projets",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "BilanCloture",
                table: "Projets",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "BilanBudget",
                table: "Projets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BilanDifficultes",
                table: "Projets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BilanPerimetre",
                table: "Projets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BilanPlanning",
                table: "Projets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BilanReussites",
                table: "Projets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeconsEchecs",
                table: "Projets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeconsRecommandations",
                table: "Projets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeconsReussites",
                table: "Projets",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfilRessource",
                table: "Utilisateurs");

            migrationBuilder.DropColumn(
                name: "BilanBudget",
                table: "Projets");

            migrationBuilder.DropColumn(
                name: "BilanDifficultes",
                table: "Projets");

            migrationBuilder.DropColumn(
                name: "BilanPerimetre",
                table: "Projets");

            migrationBuilder.DropColumn(
                name: "BilanPlanning",
                table: "Projets");

            migrationBuilder.DropColumn(
                name: "BilanReussites",
                table: "Projets");

            migrationBuilder.DropColumn(
                name: "LeconsEchecs",
                table: "Projets");

            migrationBuilder.DropColumn(
                name: "LeconsRecommandations",
                table: "Projets");

            migrationBuilder.DropColumn(
                name: "LeconsReussites",
                table: "Projets");

            migrationBuilder.AlterColumn<string>(
                name: "LeconsApprises",
                table: "Projets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BilanCloture",
                table: "Projets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
