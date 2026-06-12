using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjects.Migrations
{
    /// <inheritdoc />
    public partial class FixUniqueIndexUtilisateurRoleFiltered : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UtilisateurRoles_UtilisateurId_Role",
                table: "UtilisateurRoles");

            migrationBuilder.CreateIndex(
                name: "IX_UtilisateurRoles_UtilisateurId_Role",
                table: "UtilisateurRoles",
                columns: new[] { "UtilisateurId", "Role" },
                unique: true,
                filter: "[EstSupprime] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UtilisateurRoles_UtilisateurId_Role",
                table: "UtilisateurRoles");

            migrationBuilder.CreateIndex(
                name: "IX_UtilisateurRoles_UtilisateurId_Role",
                table: "UtilisateurRoles",
                columns: new[] { "UtilisateurId", "Role" },
                unique: true);
        }
    }
}
