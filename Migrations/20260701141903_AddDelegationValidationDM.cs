using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjects.Migrations
{
    /// <inheritdoc />
    public partial class AddDelegationValidationDM : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DelegationsValidationDM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirecteurMetierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DelegueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateDebut = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateFin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstActive = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationsValidationDM", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DelegationsValidationDM_Utilisateurs_DelegueId",
                        column: x => x.DelegueId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DelegationsValidationDM_Utilisateurs_DirecteurMetierId",
                        column: x => x.DirecteurMetierId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DelegationsValidationDM_DelegueId",
                table: "DelegationsValidationDM",
                column: "DelegueId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationsValidationDM_DirecteurMetierId",
                table: "DelegationsValidationDM",
                column: "DirecteurMetierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DelegationsValidationDM");
        }
    }
}
