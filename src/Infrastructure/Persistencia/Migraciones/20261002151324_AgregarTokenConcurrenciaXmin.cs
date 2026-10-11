using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarTokenConcurrenciaXmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "xmin" ya existe como columna de sistema en toda tabla de Postgres;
            // esta migración solo actualiza el modelo/snapshot de EF Core para
            // que coincida con su uso como token de concurrencia. No hay cambio
            // de esquema real que aplicar.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
