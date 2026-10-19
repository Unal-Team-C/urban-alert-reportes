using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.DTOs.Reportes;
using Application.Comun;
using Application.Reportes.Eventos;
using Domain.Reportes;
using Infrastructure.Mensajeria.Outbox;
using Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntegrationTests.Mensajeria;

// Spec 002 RF-002 a RF-005 y RNF-005, con el despachador invocado a mano.
[Collection(ColeccionOutbox.Nombre)]
public class DespachadorOutboxTests(OutboxApiFactory factory) : IClassFixture<OutboxApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(CodigoEnum.Politica, allowIntegerValues: false) }
    };

    public async Task InitializeAsync()
    {
        factory.Publicador.FallarTodo = false;
        factory.Publicador.FallarPara.Clear();
        await factory.VaciarPendientesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ConElBusCaido_LaCreacionResponde201_YElEventoSeEntregaAlRecuperarse()
    {
        factory.Publicador.FallarTodo = true;

        HttpResponseMessage respuesta = await factory.CreateClient().PostAsJsonAsync("/api/v1/Reportes", new CrearReporteRequest(
            CategoriaReporte.ViasYAndenes,
            TipoReporte.HuecosEnLaVia,
            "Hueco grande que afecta el tránsito vehicular",
            4.6512,
            -74.0561,
            "https://imagenes.urbanalert.com/foto.jpg"), Json);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Guid idReporte = (await respuesta.Content.ReadFromJsonAsync<CrearReporteRespuesta>())!.IdReporte;
        Guid idEvento = await IdEventoDelReporteAsync(idReporte);

        Assert.Equal(1, await factory.Despachador.ProcesarLoteAsync(CancellationToken.None));
        MensajeOutbox trasFallo = await factory.MensajeAsync(idEvento);
        Assert.Equal(EstadoMensajeOutbox.Pendiente, trasFallo.Estado);
        Assert.Equal(1, trasFallo.Intentos);
        Assert.Contains("bus no disponible", trasFallo.UltimoError);

        // Antes de la espera del backoff (2 s) no se reintenta.
        Assert.Equal(0, await factory.Despachador.ProcesarLoteAsync(CancellationToken.None));

        factory.Publicador.FallarTodo = false;
        factory.Reloj.Avanzar(TimeSpan.FromSeconds(3));
        Assert.Equal(1, await factory.Despachador.ProcesarLoteAsync(CancellationToken.None));

        MensajeOutbox entregado = await factory.MensajeAsync(idEvento);
        Assert.Equal(EstadoMensajeOutbox.Entregado, entregado.Estado);
        Assert.NotNull(entregado.EntregadoEn);
        Assert.Single(factory.Publicador.Publicados, idEvento);
    }

    [Fact]
    public async Task UnEventoQueAgotaLosIntentos_QuedaFallido_YNoBloqueaALosDemas()
    {
        ReporteCreadoEvent problematico = await factory.CrearReporteAsync();
        ReporteCreadoEvent sano = await factory.CrearReporteAsync();
        factory.Publicador.FallarPara[problematico.IdEvento] = true;

        for (int intento = 0; intento < OutboxApiFactory.MaxIntentos; intento++)
        {
            await factory.Despachador.ProcesarLoteAsync(CancellationToken.None);
            factory.Reloj.Avanzar(TimeSpan.FromMinutes(10));
        }

        MensajeOutbox fallido = await factory.MensajeAsync(problematico.IdEvento);
        Assert.Equal(EstadoMensajeOutbox.Fallido, fallido.Estado);
        Assert.Equal(OutboxApiFactory.MaxIntentos, fallido.Intentos);
        Assert.Equal(EstadoMensajeOutbox.Entregado, (await factory.MensajeAsync(sano.IdEvento)).Estado);

        // Ya no se vuelve a intentar, y queda en el log como error (RF-013).
        Assert.Equal(0, await factory.Despachador.ProcesarLoteAsync(CancellationToken.None));
        Assert.Contains(factory.Logs.Entradas, e => e.Nivel == LogLevel.Error && e.Mensaje.Contains(problematico.IdEvento.ToString()) && e.Mensaje.Contains("FALLIDO"));
    }

    [Fact]
    public async Task DosDespachadoresConcurrentes_EntreganCadaEventoUnaSolaVez()
    {
        List<Guid> ids = [];
        for (int i = 0; i < 30; i++)
            ids.Add((await factory.CrearReporteAsync()).IdEvento);

        // Dos tareas de ECS a la vez: SKIP LOCKED reparte las filas sin repetir ninguna.
        int pendientes;
        do
        {
            int[] procesados = await Task.WhenAll(
                Task.Run(() => factory.Despachador.ProcesarLoteAsync(CancellationToken.None)),
                Task.Run(() => factory.Despachador.ProcesarLoteAsync(CancellationToken.None)));
            pendientes = procesados.Sum();
        } while (pendientes > 0);

        IReadOnlyList<Guid> publicados = factory.Publicador.Publicados;
        foreach (Guid id in ids)
            Assert.Single(publicados, id);
    }

    private async Task<Guid> IdEventoDelReporteAsync(Guid idReporte)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        List<MensajeOutbox> mensajes = await scope.ServiceProvider.GetRequiredService<ReportesDbContext>().MensajesOutbox
            .AsNoTracking().Where(m => m.Estado == EstadoMensajeOutbox.Pendiente).ToListAsync();
        return Assert.Single(mensajes,
            m => ((ReporteCreadoEvent)SerializacionEventos.Deserializar(m.Tipo, m.Contenido)).Reporte.Id == idReporte).Id;
    }

    private record CrearReporteRespuesta(Guid IdReporte, string Message);
}

// Spec 002 RNF-001: con el despachador en segundo plano y RabbitMQ real, el evento se entrega en menos de 5 s.
[Collection(ColeccionOutbox.Nombre)]
public class DespachadorEnSegundoPlanoTests(ReportesApiFactory factory) : IClassFixture<ReportesApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(CodigoEnum.Politica, allowIntegerValues: false) }
    };

    [Fact]
    public async Task ElEventoDeUnReporteCreadoSeEntregaEnMenosDe5Segundos()
    {
        HttpResponseMessage respuesta = await factory.CreateClient().PostAsJsonAsync("/api/v1/Reportes", new CrearReporteRequest(
            CategoriaReporte.ViasYAndenes,
            TipoReporte.HuecosEnLaVia,
            "Hueco grande que afecta el tránsito vehicular",
            4.6512,
            -74.0561,
            "https://imagenes.urbanalert.com/foto.jpg"), Json);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Guid idReporte = (await respuesta.Content.ReadFromJsonAsync<CrearReporteRespuesta>())!.IdReporte;

        DateTime limite = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < limite)
        {
            using IServiceScope scope = factory.Services.CreateScope();
            List<MensajeOutbox> entregados = await scope.ServiceProvider.GetRequiredService<ReportesDbContext>().MensajesOutbox
                .AsNoTracking().Where(m => m.Estado == EstadoMensajeOutbox.Entregado && m.EntregadoEn > DateTime.UtcNow.AddMinutes(-1))
                .ToListAsync();
            if (entregados.Any(m => ((ReporteCreadoEvent)SerializacionEventos.Deserializar(m.Tipo, m.Contenido)).Reporte.Id == idReporte))
                return;

            await Task.Delay(200);
        }

        Assert.Fail("El evento ReporteCreado no se entregó en 5 s.");
    }

    private record CrearReporteRespuesta(Guid IdReporte, string Message);
}
