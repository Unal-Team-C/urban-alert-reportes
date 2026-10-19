using System.Text.Json;
using Amazon;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Runtime;
using Application.Reportes.Eventos;
using Domain.Reportes;
using Infrastructure.Mensajeria;

namespace UnitTests.Infrastructure;

public class EventBridgeEventPublisherTests
{
    private const string Bus = "arn:aws:events:us-east-2:348364742283:event-bus/urban-alert-bus-eventos";

    private static ReporteCreadoEvent Evento() => new(
        Guid.Parse("0199d2f1-0000-7000-8000-000000000001"),
        new ReporteEventoDto(
            Guid.Parse("0199d2f1-0000-7000-8000-000000000002"),
            EstadoReporte.Reportado,
            Guid.Parse("0199d2f1-0000-7000-8000-000000000003"),
            Guid.Parse("0199d2f1-0000-7000-8000-000000000004"),
            new DateTime(2026, 10, 11, 15, 30, 0, DateTimeKind.Utc)));

    [Fact]
    public async Task PublicarAsync_EnviaUnaEntradaAlBusConSourceYDetailType()
    {
        EventBridgeFalso eventBridge = new();

        await new EventBridgeEventPublisher(eventBridge, new EventBridgeOpciones(Bus))
            .PublicarAsync(Evento(), CancellationToken.None);

        PutEventsRequest solicitud = Assert.Single(eventBridge.Solicitudes);
        PutEventsRequestEntry entrada = Assert.Single(solicitud.Entries);
        Assert.Equal(Bus, entrada.EventBusName);
        Assert.Equal("urban-alert.reportes", entrada.Source);
        Assert.Equal("ReporteCreado", entrada.DetailType);
    }

    [Fact]
    public async Task PublicarAsync_SerializaElEventoEnCamelCaseConEnumsComoCodigos()
    {
        EventBridgeFalso eventBridge = new();

        await new EventBridgeEventPublisher(eventBridge, new EventBridgeOpciones(Bus))
            .PublicarAsync(Evento(), CancellationToken.None);

        using JsonDocument detalle = JsonDocument.Parse(eventBridge.Solicitudes[0].Entries[0].Detail);
        JsonElement raiz = detalle.RootElement;
        Assert.Equal("0199d2f1-0000-7000-8000-000000000001", raiz.GetProperty("idEvento").GetString());
        JsonElement reporte = raiz.GetProperty("reporte");
        Assert.Equal("0199d2f1-0000-7000-8000-000000000002", reporte.GetProperty("id").GetString());
        Assert.Equal("REPORTADO", reporte.GetProperty("estado").GetString());
        Assert.Equal("0199d2f1-0000-7000-8000-000000000003", reporte.GetProperty("idCoordenada").GetString());
        Assert.Equal("0199d2f1-0000-7000-8000-000000000004", reporte.GetProperty("idUsuario").GetString());
        Assert.Equal(new DateTime(2026, 10, 11, 15, 30, 0, DateTimeKind.Utc), reporte.GetProperty("fecha").GetDateTime());
    }

    // No regresión (spec 002 RF-006): el despachador del outbox publica el evento como object.
    [Fact]
    public async Task PublicarAsync_ComoObject_EnviaElMismoDetailTypeYDetail()
    {
        EventBridgeFalso tipado = new();
        EventBridgeFalso comoObject = new();

        await new EventBridgeEventPublisher(tipado, new EventBridgeOpciones(Bus)).PublicarAsync(Evento(), CancellationToken.None);
        object evento = Evento();
        await new EventBridgeEventPublisher(comoObject, new EventBridgeOpciones(Bus)).PublicarAsync(evento, CancellationToken.None);

        PutEventsRequestEntry esperado = tipado.Solicitudes[0].Entries[0];
        PutEventsRequestEntry obtenido = comoObject.Solicitudes[0].Entries[0];
        Assert.Equal("ReporteCreado", obtenido.DetailType);
        Assert.Equal(esperado.Source, obtenido.Source);
        Assert.Equal(esperado.Detail, obtenido.Detail);
    }

    [Fact]
    public async Task PublicarAsync_FallaSiEventBridgeRechazaLaEntrada()
    {
        EventBridgeFalso eventBridge = new()
        {
            Respuesta = new PutEventsResponse
            {
                FailedEntryCount = 1,
                Entries = [new PutEventsResultEntry { ErrorCode = "AccessDeniedException", ErrorMessage = "Not authorized" }]
            }
        };

        EventBridgeEventPublisher publicador = new(eventBridge, new EventBridgeOpciones(Bus));

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => publicador.PublicarAsync(Evento(), CancellationToken.None));
        Assert.Contains("AccessDeniedException", error.Message);
    }

    // Cliente real del SDK con el envío sustituido: no sale ninguna petición.
    private sealed class EventBridgeFalso()
        : AmazonEventBridgeClient(new BasicAWSCredentials("AKIDEXAMPLE", "secreto"), RegionEndpoint.USEast2)
    {
        public List<PutEventsRequest> Solicitudes { get; } = [];

        public PutEventsResponse Respuesta { get; init; } = new()
        {
            FailedEntryCount = 0,
            Entries = [new PutEventsResultEntry { EventId = "11111111-2222-3333-4444-555555555555" }]
        };

        public override Task<PutEventsResponse> PutEventsAsync(PutEventsRequest request, CancellationToken cancellationToken = default)
        {
            Solicitudes.Add(request);
            return Task.FromResult(Respuesta);
        }
    }
}
