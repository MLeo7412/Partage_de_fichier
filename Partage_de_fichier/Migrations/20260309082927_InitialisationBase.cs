using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Partage_de_fichier.Migrations
{
    /// <inheritdoc />
    public partial class InitialisationBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Utilisateurs",
                columns: table => new
                {
                    IdUtilisateur = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NomUtilisateur = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MotDePasseHash = table.Column<string>(type: "text", nullable: false),
                    ClePubliqueRsa = table.Column<string>(type: "text", nullable: false),
                    ClePriveeRsaChiffree = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utilisateurs", x => x.IdUtilisateur);
                });

            migrationBuilder.CreateTable(
                name: "Fichiers",
                columns: table => new
                {
                    IdFichier = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NomFichier = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CheminServeur = table.Column<string>(type: "text", nullable: false),
                    IvAes = table.Column<string>(type: "text", nullable: false),
                    IdProprietaire = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fichiers", x => x.IdFichier);
                    table.ForeignKey(
                        name: "FK_Fichiers_Utilisateurs_IdProprietaire",
                        column: x => x.IdProprietaire,
                        principalTable: "Utilisateurs",
                        principalColumn: "IdUtilisateur",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartagesAcces",
                columns: table => new
                {
                    IdPartage = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdFichier = table.Column<int>(type: "integer", nullable: false),
                    IdUtilisateur = table.Column<int>(type: "integer", nullable: false),
                    CleAesChiffree = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartagesAcces", x => x.IdPartage);
                    table.ForeignKey(
                        name: "FK_PartagesAcces_Fichiers_IdFichier",
                        column: x => x.IdFichier,
                        principalTable: "Fichiers",
                        principalColumn: "IdFichier",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartagesAcces_Utilisateurs_IdUtilisateur",
                        column: x => x.IdUtilisateur,
                        principalTable: "Utilisateurs",
                        principalColumn: "IdUtilisateur",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Fichiers_IdProprietaire",
                table: "Fichiers",
                column: "IdProprietaire");

            migrationBuilder.CreateIndex(
                name: "IX_PartagesAcces_IdFichier",
                table: "PartagesAcces",
                column: "IdFichier");

            migrationBuilder.CreateIndex(
                name: "IX_PartagesAcces_IdUtilisateur",
                table: "PartagesAcces",
                column: "IdUtilisateur");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartagesAcces");

            migrationBuilder.DropTable(
                name: "Fichiers");

            migrationBuilder.DropTable(
                name: "Utilisateurs");
        }
    }
}
