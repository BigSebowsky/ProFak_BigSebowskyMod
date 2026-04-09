using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProFak.DB.Migrations
{
    /// <inheritdoc />
    public partial class KSeFZakupInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KSeFZakupInbox",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NumerKSeF = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    Numer = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    DataWystawienia = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataSprzedazy = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataKSeF = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataPobrania = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataWeryfikacji = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataDodaniaJakoZakup = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataRozliczenia = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NazwaSprzedawcy = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    NIPSprzedawcy = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    NazwaNabywcy = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    NIPNabywcy = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    RazemNetto = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 0m),
                    RazemVat = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 0m),
                    RazemBrutto = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 0m),
                    Waluta = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    TypDokumentu = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    XMLKSeF = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    URLKSeF = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    CzyNowa = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    FakturaId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KSeFZakupInbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KSeFZakupInbox_Faktura_FakturaId",
                        column: x => x.FakturaId,
                        principalTable: "Faktura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "KSeFZakupInboxStan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CzyAutoSynchronizacja = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    InterwalSynchronizacjiMinuty = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 15),
                    DataPoczatkowaSynchronizacji = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValue: new DateTime(2026, 2, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)),
                    DataOstatniejSynchronizacji = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataNastepnejSynchronizacji = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LiczbaNowychDokumentow = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KSeFZakupInboxStan", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KSeFZakupInbox_FakturaId",
                table: "KSeFZakupInbox",
                column: "FakturaId");

            migrationBuilder.CreateIndex(
                name: "IX_KSeFZakupInbox_NumerKSeF",
                table: "KSeFZakupInbox",
                column: "NumerKSeF",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KSeFZakupInbox");

            migrationBuilder.DropTable(
                name: "KSeFZakupInboxStan");
        }
    }
}
