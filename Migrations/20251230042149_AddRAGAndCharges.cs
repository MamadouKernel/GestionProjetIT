using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjects.Migrations
{
    /// <inheritdoc />
    public partial class AddRAGAndCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateDernierCalculRAG",
                table: "Projets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IndicateurRAG",
                table: "Projets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ChargesProjets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RessourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SemaineDebut = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChargePrevisionnelle = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ChargeReelle = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DateSaisieChargeReelle = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SaisieParId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Commentaire = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProjetId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreePar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiePar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargesProjets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChargesProjets_Projets_ProjetId",
                        column: x => x.ProjetId,
                        principalTable: "Projets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChargesProjets_Projets_ProjetId1",
                        column: x => x.ProjetId1,
                        principalTable: "Projets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChargesProjets_Utilisateurs_RessourceId",
                        column: x => x.RessourceId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChargesProjets_Utilisateurs_SaisieParId",
                        column: x => x.SaisieParId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChargesProjets_ProjetId_SemaineDebut_RessourceId",
                table: "ChargesProjets",
                columns: new[] { "ProjetId", "SemaineDebut", "RessourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChargesProjets_ProjetId1",
                table: "ChargesProjets",
                column: "ProjetId1");

            migrationBuilder.CreateIndex(
                name: "IX_ChargesProjets_RessourceId",
                table: "ChargesProjets",
                column: "RessourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargesProjets_SaisieParId",
                table: "ChargesProjets",
                column: "SaisieParId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChargesProjets");

            migrationBuilder.DropColumn(
                name: "DateDernierCalculRAG",
                table: "Projets");

            migrationBuilder.DropColumn(
                name: "IndicateurRAG",
                table: "Projets");
        }
    }
}
