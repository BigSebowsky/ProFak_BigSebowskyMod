using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProFak.DB.Migrations
{
    /// <inheritdoc />
    public partial class KrajeKontrahentowIRachunkow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KrajId",
                table: "RachunekBankowy",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KrajId",
                table: "Kontrahent",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Kraj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KodISO2 = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    Nazwa = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    CzyUE = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kraj", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RachunekBankowy_KrajId",
                table: "RachunekBankowy",
                column: "KrajId");

            migrationBuilder.CreateIndex(
                name: "IX_Kontrahent_KrajId",
                table: "Kontrahent",
                column: "KrajId");

            migrationBuilder.CreateIndex(
                name: "IX_Kraj_KodISO2",
                table: "Kraj",
                column: "KodISO2",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Kontrahent_Kraj_KrajId",
                table: "Kontrahent",
                column: "KrajId",
                principalTable: "Kraj",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RachunekBankowy_Kraj_KrajId",
                table: "RachunekBankowy",
                column: "KrajId",
                principalTable: "Kraj",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Kontrahent_Kraj_KrajId",
                table: "Kontrahent");

            migrationBuilder.DropForeignKey(
                name: "FK_RachunekBankowy_Kraj_KrajId",
                table: "RachunekBankowy");

            migrationBuilder.DropTable(
                name: "Kraj");

            migrationBuilder.DropIndex(
                name: "IX_RachunekBankowy_KrajId",
                table: "RachunekBankowy");

            migrationBuilder.DropIndex(
                name: "IX_Kontrahent_KrajId",
                table: "Kontrahent");

            migrationBuilder.DropColumn(
                name: "KrajId",
                table: "RachunekBankowy");

            migrationBuilder.DropColumn(
                name: "KrajId",
                table: "Kontrahent");
        }
    }
}
