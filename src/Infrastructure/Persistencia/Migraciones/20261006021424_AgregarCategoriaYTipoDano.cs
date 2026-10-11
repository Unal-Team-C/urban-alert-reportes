using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarCategoriaYTipoDano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Antes TipoDano era texto libre: ningún valor existente corresponde a
            // un código del catálogo, así que los reportes previos quedan como
            // Otros/Otros. También evita que el texto largo rompa el ALTER a 50.
            migrationBuilder.Sql("""UPDATE "Reportes" SET "TipoDano" = 'Otros';""");

            migrationBuilder.AlterColumn<string>(
                name: "TipoDano",
                table: "Reportes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "Reportes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Otros");

            // El valor por defecto solo sirve para rellenar las filas existentes;
            // los reportes nuevos siempre envían su categoría.
            migrationBuilder.Sql("""ALTER TABLE "Reportes" ALTER COLUMN "Categoria" DROP DEFAULT;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "Reportes");

            migrationBuilder.AlterColumn<string>(
                name: "TipoDano",
                table: "Reportes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}
