using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProFak.DB.Migrations
{
    /// <inheritdoc />
    public partial class RachunkiBankoweKontrahenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RachunekBankowy",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KontrahentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nazwa = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    NumerRachunku = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    NazwaBanku = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    Swift = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    WalutaId = table.Column<int>(type: "INTEGER", nullable: true),
                    CzyDomyslny = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RachunekBankowy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RachunekBankowy_Kontrahent_KontrahentId",
                        column: x => x.KontrahentId,
                        principalTable: "Kontrahent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RachunekBankowy_Waluta_WalutaId",
                        column: x => x.WalutaId,
                        principalTable: "Waluta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RachunekBankowy_KontrahentId",
                table: "RachunekBankowy",
                column: "KontrahentId");

            migrationBuilder.CreateIndex(
                name: "IX_RachunekBankowy_WalutaId",
                table: "RachunekBankowy",
                column: "WalutaId");

            migrationBuilder.Sql("""
                INSERT INTO "RachunekBankowy" ("KontrahentId", "Nazwa", "NumerRachunku", "NazwaBanku", "Swift", "WalutaId", "CzyDomyslny")
                SELECT
                    "Id",
                    '',
                    COALESCE("RachunekBankowy", ''),
                    COALESCE("NazwaBanku", ''),
                    '',
                    "DomyslnaWalutaId",
                    1
                FROM "Kontrahent"
                WHERE TRIM(COALESCE("RachunekBankowy", '')) <> ''
                   OR TRIM(COALESCE("NazwaBanku", '')) <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RachunekBankowy");
        }
    }
}
