using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.DTOs.Reportes;
using Application.Comun;
using Application.Interfaces.Reportes;
using Application.Reportes.Eventos;
using Domain.Reportes;
using Infrastructure.Mensajeria.Outbox;
using Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Reportes;

// Spec 002 RF-001: el reporte y su evento de creación se guardan en la misma transacción.
public class OutboxGuardadoTests(ReportesApiFactory factory) : IClassFixture<ReportesApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(CodigoEnum.Politica, allowIntegerValues: false) }
    };

    [Fact]
    public async Task Crear_GuardaElEventoReporteCreadoEnElOutbox()
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

        using IServiceScope scope = factory.Services.CreateScope();
        ReportesDbContext db = scope.ServiceProvider.GetRequiredService<ReportesDbContext>();
        List<MensajeOutbox> mensajes = await db.MensajesOutbox.AsNoTracking()
            .Where(m => m.Tipo == nameof(ReporteCreadoEvent))
            .ToListAsync();

        MensajeOutbox mensaje = Assert.Single(mensajes,
            m => ((ReporteCreadoEvent)SerializacionEventos.Deserializar(m.Tipo, m.Contenido)).Reporte.Id == idReporte);
        Assert.NotEqual(EstadoMensajeOutbox.Fallido, mensaje.Estado);
    }

    [Fact]
    public async Task Agregar_NoDejaEventoSiElReporteNoSePuedeGuardar()
    {
        Reporte reporte = ReporteValido();
        using (IServiceScope scopeSemilla = factory.Services.CreateScope())
        {
            await scopeSemilla.ServiceProvider.GetRequiredService<IReporteRepository>()
                .AgregarAsync(reporte, EventoDe(reporte), CancellationToken.None);
        }

        // Mismo Id de reporte: la clave primaria rechaza el INSERT y la transacción completa se revierte.
        ReporteCreadoEvent segundoEvento = EventoDe(reporte);
        using (IServiceScope scopeDuplicado = factory.Services.CreateScope())
        {
            IReporteRepository repositorio = scopeDuplicado.ServiceProvider.GetRequiredService<IReporteRepository>();
            await Assert.ThrowsAsync<DbUpdateException>(
                () => repositorio.AgregarAsync(reporte, segundoEvento, CancellationToken.None));
        }

        using IServiceScope scope = factory.Services.CreateScope();
        ReportesDbContext db = scope.ServiceProvider.GetRequiredService<ReportesDbContext>();
        Assert.False(await db.MensajesOutbox.AnyAsync(m => m.Id == segundoEvento.IdEvento));
    }

    private record CrearReporteRespuesta(Guid IdReporte, string Message);

    private static Reporte ReporteValido()
    {
        Reporte reporte = new(
            CategoriaReporte.ViasYAndenes,
            TipoReporte.HuecosEnLaVia,
            "Hueco grande que afecta el tránsito vehicular",
            "https://imagenes.urbanalert.com/foto.jpg",
            null,
            Guid.NewGuid());
        reporte.AsignarCoordenada(Guid.NewGuid());
        return reporte;
    }

    private static ReporteCreadoEvent EventoDe(Reporte reporte) => new(
        Guid.CreateVersion7(),
        new ReporteEventoDto(reporte.Id, reporte.Estado, reporte.IdCoordenada, reporte.IdUsuario, reporte.Fecha));
}
