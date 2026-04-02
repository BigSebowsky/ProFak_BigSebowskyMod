using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProFak.DB.Migrations
{
    public partial class SzablonFaktury : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SzablonFaktury",
                table: "Konfiguracja",
                type: "TEXT",
                nullable: false,
                defaultValue: "Faktura");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SzablonFaktury",
                table: "Konfiguracja");
        }
    }
}
