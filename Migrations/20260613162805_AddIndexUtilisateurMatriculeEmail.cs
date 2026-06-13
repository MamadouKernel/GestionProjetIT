using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjects.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexUtilisateurMatriculeEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Matricule",
                table: "Utilisateurs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Utilisateurs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.CreateIndex(
                name: "IX_Utilisateurs_Email",
                table: "Utilisateurs",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Utilisateurs_Matricule",
                table: "Utilisateurs",
                column: "Matricule",
                unique: true,
                filter: "[EstSupprime] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Utilisateurs_Email",
                table: "Utilisateurs");

            migrationBuilder.DropIndex(
                name: "IX_Utilisateurs_Matricule",
                table: "Utilisateurs");

            migrationBuilder.AlterColumn<string>(
                name: "Matricule",
                table: "Utilisateurs",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Utilisateurs",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);
        }
    }
}
