using System.Collections.Concurrent;
using Application.Interfaces.Eventos;
using Application.Interfaces.Reportes;
using Application.Reportes.Eventos;
using Domain.Reportes;
using Infrastructure.Mensajeria.Outbox;
using Infrastructure.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace IntegrationTests.Mensajeria;

// Las pruebas del despachador comparten la tabla del outbox: corren solas, sin otros hosts con su propio despachador.
[CollectionDefinition(Nombre, DisableParallelization = true)]
public class ColeccionOutbox
{
    public const string Nombre = "Outbox";
}

// Host sin despachador en segundo plano: las pruebas lo invocan a mano, con un publicador y un reloj controlables.
public class OutboxApiFactory : ReportesApiFactory
{
    public const int MaxIntentos = 3;

    public PublicadorControlable Publicador { get; } = new();
    public RelojControlable Reloj { get; } = new(DateTimeOffset.UtcNow);
    public LogsCapturados Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Outbox:Habilitado", "false");
        builder.UseSetting("Outbox:MaxIntentos", MaxIntentos.ToString());
        builder.ConfigureLogging(logs => logs.AddProvider(Logs));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEventPublisher>().AddSingleton<IEventPublisher>(Publicador);
            services.RemoveAll<TimeProvider>().AddSingleton<TimeProvider>(Reloj);
        });
    }

    public DespachadorOutbox Despachador => Services.GetRequiredService<DespachadorOutbox>();

    // Deja la tabla sin pendientes de otras pruebas para que cada una vea solo sus filas.
    public async Task VaciarPendientesAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ReportesDbContext>().Database
            .ExecuteSqlRawAsync("""UPDATE "MensajesOutbox" SET "Estado" = 'ENTREGADO' WHERE "Estado" = 'PENDIENTE'""");
    }

    public async Task<ReporteCreadoEvent> CrearReporteAsync()
    {
        Reporte reporte = new(
            CategoriaReporte.ViasYAndenes,
            TipoReporte.HuecosEnLaVia,
            "Hueco grande que afecta el tránsito vehicular",
            "https://imagenes.urbanalert.com/foto.jpg",
            null,
            Guid.NewGuid());
        reporte.AsignarCoordenada(Guid.NewGuid());
        ReporteCreadoEvent evento = new(
            Guid.CreateVersion7(),
            new ReporteEventoDto(reporte.Id, reporte.Estado, reporte.IdCoordenada, reporte.IdUsuario, reporte.Fecha));

        using IServiceScope scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IReporteRepository>().AgregarAsync(reporte, evento, CancellationToken.None);
        return evento;
    }

    public async Task<MensajeOutbox> MensajeAsync(Guid idEvento)
    {
        using IServiceScope scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReportesDbContext>().MensajesOutbox
            .AsNoTracking().SingleAsync(m => m.Id == idEvento);
    }
}

public sealed class PublicadorControlable : IEventPublisher
{
    private readonly ConcurrentQueue<Guid> _publicados = new();

    public bool FallarTodo { get; set; }
    public ConcurrentDictionary<Guid, bool> FallarPara { get; } = new();

    public IReadOnlyList<Guid> Publicados => _publicados.ToArray();

    public Task PublicarAsync(object evento, CancellationToken cancellationToken)
    {
        Guid id = ((IEventoDominio)evento).IdEvento;
        if (FallarTodo || FallarPara.ContainsKey(id))
            throw new InvalidOperationException("bus no disponible (prueba)");

        _publicados.Enqueue(id);
        return Task.CompletedTask;
    }
}

public sealed class RelojControlable(DateTimeOffset inicio) : TimeProvider
{
    private DateTimeOffset _ahora = inicio;

    public override DateTimeOffset GetUtcNow() => _ahora;

    public void Avanzar(TimeSpan tiempo) => _ahora += tiempo;
}

public sealed class LogsCapturados : ILoggerProvider
{
    private readonly ConcurrentQueue<(LogLevel Nivel, string Mensaje)> _entradas = new();

    public IReadOnlyList<(LogLevel Nivel, string Mensaje)> Entradas => _entradas.ToArray();

    public ILogger CreateLogger(string categoryName) => new Registrador(_entradas);

    public void Dispose()
    {
    }

    private sealed class Registrador(ConcurrentQueue<(LogLevel, string)> entradas) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entradas.Enqueue((logLevel, formatter(state, exception)));
    }
}
