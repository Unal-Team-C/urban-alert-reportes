using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MensajesOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Contenido = table.Column<string>(type: "jsonb", nullable: false),
                    RegistradoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Intentos = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ProximoIntento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntregadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UltimoError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensajesOutbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MensajesOutbox_Pendientes",
                table: "MensajesOutbox",
                column: "ProximoIntento",
                filter: "\"Estado\" = 'PENDIENTE'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MensajesOutbox");
        }
    }
}
