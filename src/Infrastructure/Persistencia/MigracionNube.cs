using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.Persistencia;

public sealed record MigracionNubeOpciones(string CadenaAdmin, string UsuarioApp, string ContrasenaApp);

// Esquema en AWS (tarea puntual "API.dll --migrar", spec RF-008/RF-009): aplica las migraciones de EF Core
// como administrador y deja el rol de la aplicación con permisos de datos y sin DDL. Es idempotente: se
// ejecuta en cada despliegue, crea el rol si falta y vuelve a sincronizar su contraseña y sus permisos.
public static class MigracionNube
{
    // La contraseña llega por set_config y se escapa con format('%L') dentro del servidor: nunca se
    // interpola en el texto SQL ni se registra.
    private const string SqlRolApp = """
        DO $$
        DECLARE
            usuario text := current_setting('reportes.usuario_app');
            contrasena text := current_setting('reportes.contrasena_app');
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = usuario) THEN
                EXECUTE format('CREATE ROLE %I LOGIN', usuario);
            END IF;
            EXECUTE format('ALTER ROLE %I WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD %L', usuario, contrasena);
            EXECUTE format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), usuario);
            EXECUTE format('GRANT USAGE ON SCHEMA public TO %I', usuario);
            EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO %I', usuario);
            EXECUTE format('GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO %I', usuario);
            EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I', usuario);
            EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO %I', usuario);
            REVOKE CREATE ON SCHEMA public FROM PUBLIC;
        END
        $$;
        """;

    public static async Task EjecutarAsync(MigracionNubeOpciones opciones, ILogger logger, CancellationToken cancellationToken)
    {
        DbContextOptions<ReportesDbContext> opcionesContexto = new DbContextOptionsBuilder<ReportesDbContext>()
            .UseNpgsql(opciones.CadenaAdmin)
            .Options;

        await using (ReportesDbContext contexto = new(opcionesContexto))
        {
            List<string> pendientes = (await contexto.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            logger.LogInformation("Migraciones pendientes: {Cantidad} ({Migraciones})", pendientes.Count, string.Join(", ", pendientes));
            await contexto.Database.MigrateAsync(cancellationToken);
        }

        await using NpgsqlConnection conexion = new(opciones.CadenaAdmin);
        await conexion.OpenAsync(cancellationToken);
        await using NpgsqlTransaction transaccion = await conexion.BeginTransactionAsync(cancellationToken);

        await using (NpgsqlCommand parametros = new(
            "SELECT set_config('reportes.usuario_app', @usuario, true), set_config('reportes.contrasena_app', @contrasena, true)",
            conexion, transaccion))
        {
            parametros.Parameters.AddWithValue("usuario", opciones.UsuarioApp);
            parametros.Parameters.AddWithValue("contrasena", opciones.ContrasenaApp);
            await parametros.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (NpgsqlCommand rol = new(SqlRolApp, conexion, transaccion))
            await rol.ExecuteNonQueryAsync(cancellationToken);

        await transaccion.CommitAsync(cancellationToken);
        logger.LogInformation("Rol de la aplicación {Usuario} sincronizado", opciones.UsuarioApp);
    }
}
