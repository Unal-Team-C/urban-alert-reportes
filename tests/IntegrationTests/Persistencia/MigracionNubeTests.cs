using Infrastructure.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace IntegrationTests.Persistencia;

// Modo --migrar (tarea puntual en AWS): aplica las migraciones como administrador y deja el rol de la
// aplicación con permisos de datos y sin DDL. Usa el PostgreSQL de las pruebas de integración, en una
// base y un rol propios de cada prueba que se eliminan al terminar.
public sealed class MigracionNubeTests : IAsyncLifetime
{
    private readonly string _cadenaServidor;
    private readonly string _base = $"reportes_migracion_{Guid.NewGuid():N}";
    private readonly string _usuarioApp = $"reportes_app_{Guid.NewGuid():N}"[..30];

    public MigracionNubeTests()
    {
        IConfiguration configuracion = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        _cadenaServidor = configuracion.GetConnectionString("Postgres")
                          ?? throw new InvalidOperationException("Falta ConnectionStrings:Postgres para las pruebas de integración.");
    }

    private string CadenaAdmin => new NpgsqlConnectionStringBuilder(_cadenaServidor) { Database = _base, Pooling = false }.ConnectionString;

    private string CadenaApp(string contrasena) => new NpgsqlConnectionStringBuilder(_cadenaServidor)
    {
        Database = _base, Username = _usuarioApp, Password = contrasena, Pooling = false
    }.ConnectionString;

    private Task MigrarAsync(string contrasenaApp) => MigracionNube.EjecutarAsync(
        new MigracionNubeOpciones(CadenaAdmin, _usuarioApp, contrasenaApp),
        NullLogger.Instance,
        CancellationToken.None);

    public async Task InitializeAsync() =>
        await EjecutarEnServidorAsync($"CREATE DATABASE \"{_base}\"");

    public async Task DisposeAsync()
    {
        await EjecutarEnServidorAsync($"DROP DATABASE IF EXISTS \"{_base}\" WITH (FORCE)");
        await EjecutarEnServidorAsync($"DROP ROLE IF EXISTS \"{_usuarioApp}\"");
    }

    [Fact]
    public async Task Migrar_AplicaTodasLasMigracionesYEsIdempotente()
    {
        await MigrarAsync("ContrasenaApp1");
        await MigrarAsync("ContrasenaApp1");

        await using NpgsqlConnection conexion = new(CadenaAdmin);
        await conexion.OpenAsync();
        long aplicadas = (long)(await new NpgsqlCommand("SELECT count(*) FROM \"__EFMigrationsHistory\"", conexion).ExecuteScalarAsync())!;
        int definidas = typeof(ReportesDbContext).Assembly.GetTypes()
            .Count(t => t.GetCustomAttributes(typeof(Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute), false).Length > 0);

        Assert.Equal(definidas, aplicadas);
    }

    [Fact]
    public async Task Migrar_DaAlRolDeLaAppPermisosDeDatosSinDdl()
    {
        await MigrarAsync("ContrasenaApp1");

        await using NpgsqlConnection conexion = new(CadenaApp("ContrasenaApp1"));
        await conexion.OpenAsync();

        foreach (string permiso in new[] { "SELECT", "INSERT", "UPDATE", "DELETE" })
        {
            bool tiene = (bool)(await new NpgsqlCommand(
                $"SELECT has_table_privilege(current_user, '\"Reportes\"', '{permiso}')", conexion).ExecuteScalarAsync())!;
            Assert.True(tiene, $"Falta {permiso} sobre Reportes");
        }

        await new NpgsqlCommand("SELECT count(*) FROM \"Reportes\"", conexion).ExecuteScalarAsync();

        PostgresException error = await Assert.ThrowsAsync<PostgresException>(
            () => new NpgsqlCommand("CREATE TABLE intruso (id int)", conexion).ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task Migrar_SincronizaLaContrasenaDelRolDeLaApp()
    {
        await MigrarAsync("ContrasenaApp1");
        await MigrarAsync("ContrasenaApp2");

        await using NpgsqlConnection nueva = new(CadenaApp("ContrasenaApp2"));
        await nueva.OpenAsync();

        await using NpgsqlConnection antigua = new(CadenaApp("ContrasenaApp1"));
        await Assert.ThrowsAsync<PostgresException>(() => antigua.OpenAsync());
    }

    private async Task EjecutarEnServidorAsync(string sql)
    {
        await using NpgsqlConnection conexion = new(new NpgsqlConnectionStringBuilder(_cadenaServidor) { Pooling = false }.ConnectionString);
        await conexion.OpenAsync();
        await new NpgsqlCommand(sql, conexion).ExecuteNonQueryAsync();
    }
}
