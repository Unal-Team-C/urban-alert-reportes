using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarNombreImagen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UrlImagen",
                table: "Reportes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<string>(
                name: "NombreImagen",
                table: "Reportes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NombreImagen",
                table: "Reportes");

            // Los reportes creados con archivo no tienen URL: se deja vacía para poder volver a NOT NULL.
            migrationBuilder.Sql("""UPDATE "Reportes" SET "UrlImagen" = '' WHERE "UrlImagen" IS NULL;""");

            migrationBuilder.AlterColumn<string>(
                name: "UrlImagen",
                table: "Reportes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}
