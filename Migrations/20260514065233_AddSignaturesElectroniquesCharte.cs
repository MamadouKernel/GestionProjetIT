using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjects.Migrations
{
    /// <inheritdoc />
    public partial class AddSignaturesElectroniquesCharte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('CharteProjets', 'DateSignatureImageCP') IS NULL
                    ALTER TABLE [CharteProjets] ADD [DateSignatureImageCP] datetime2 NULL;
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CharteProjets', 'DateSignatureImageDSI') IS NULL
                    ALTER TABLE [CharteProjets] ADD [DateSignatureImageDSI] datetime2 NULL;
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CharteProjets', 'DateSignatureImageSponsor') IS NULL
                    ALTER TABLE [CharteProjets] ADD [DateSignatureImageSponsor] datetime2 NULL;
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CharteProjets', 'SignatureImageCP') IS NULL
                    ALTER TABLE [CharteProjets] ADD [SignatureImageCP] nvarchar(max) NULL;
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CharteProjets', 'SignatureImageDSI') IS NULL
                    ALTER TABLE [CharteProjets] ADD [SignatureImageDSI] nvarchar(max) NULL;
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CharteProjets', 'SignatureImageSponsor') IS NULL
                    ALTER TABLE [CharteProjets] ADD [SignatureImageSponsor] nvarchar(max) NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('CharteProjets', 'DateSignatureImageCP') IS NOT NULL
                    ALTER TABLE [CharteProjets] DROP COLUMN [DateSignatureImageCP];
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CharteProjets', 'DateSignatureImageDSI') IS NOT NULL
                    ALTER TABLE [CharteProjets] DROP COLUMN [DateSignatureImageDSI];
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CharteProjets', 'DateSignatureImageSponsor') IS NOT NULL
                    ALTER TABLE [CharteProjets] DROP COLUMN [DateSignatureImageSponsor];
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CharteProjets', 'SignatureImageCP') IS NOT NULL
                    ALTER TABLE [CharteProjets] DROP COLUMN [SignatureImageCP];
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CharteProjets', 'SignatureImageDSI') IS NOT NULL
                    ALTER TABLE [CharteProjets] DROP COLUMN [SignatureImageDSI];
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('CharteProjets', 'SignatureImageSponsor') IS NOT NULL
                    ALTER TABLE [CharteProjets] DROP COLUMN [SignatureImageSponsor];
                """);
        }
    }
}
