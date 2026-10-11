using Infrastructure.Persistencia;
using Npgsql;

namespace API.Migracion;

// "dotnet API.dll --migrar": lo ejecuta la tarea puntual de ECS antes de desplegar cada versión (plan D-002).
// No levanta la web ni registra Geoespacial, Multimedia o mensajería. La configuración llega por entorno
// (Migracion__Host, __Base, __UsuarioAdmin, __ContrasenaAdmin, __UsuarioApp, __ContrasenaApp).
public static class ModoMigracion
{
    public const string Argumento = "--migrar";

    public static async Task<int> EjecutarAsync(string[] args)
    {
        IConfiguration configuracion = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args.Where(a => a != Argumento).ToArray())
            .Build();

        using ILoggerFactory fabricaLogs = LoggerFactory.Create(logs => logs
            .AddConfiguration(configuracion.GetSection("Logging"))
            .AddConsole());
        ILogger logger = fabricaLogs.CreateLogger("Migracion");

        try
        {
            IConfigurationSection seccion = configuracion.GetSection("Migracion");
            NpgsqlConnectionStringBuilder cadenaAdmin = new()
            {
                Host = Requerido(seccion, "Host"),
                Port = seccion.GetValue("Puerto", 5432),
                Database = Requerido(seccion, "Base"),
                Username = Requerido(seccion, "UsuarioAdmin"),
                Password = Requerido(seccion, "ContrasenaAdmin"),
                SslMode = seccion.GetValue("SslMode", SslMode.Require),
                // Sin Kerberos en la imagen: evita que Npgsql intente cifrado GSS antes de TLS.
                GssEncryptionMode = GssEncryptionMode.Disable,
                Pooling = false
            };

            await MigracionNube.EjecutarAsync(
                new MigracionNubeOpciones(cadenaAdmin.ConnectionString, Requerido(seccion, "UsuarioApp"), Requerido(seccion, "ContrasenaApp")),
                logger,
                CancellationToken.None);

            logger.LogInformation("Migración terminada");
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "La migración falló");
            return 1;
        }
    }

    private static string Requerido(IConfigurationSection seccion, string clave) =>
        seccion[clave] is { Length: > 0 } valor
            ? valor
            : throw new InvalidOperationException($"Falta la configuración 'Migracion:{clave}'.");
}
