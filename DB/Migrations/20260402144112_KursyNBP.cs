using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProFak.DB.Migrations
{
    /// <inheritdoc />
    public partial class KursyNBP : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KursNBP",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WalutaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<DateTime>(type: "TEXT", nullable: false),
                    KursSredni = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 0m),
                    NumerTabeli = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KursNBP", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KursNBP_Waluta_WalutaId",
                        column: x => x.WalutaId,
                        principalTable: "Waluta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KursNBP_WalutaId_Data",
                table: "KursNBP",
                columns: new[] { "WalutaId", "Data" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KursNBP");
        }
    }
}
