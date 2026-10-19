using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Application.Comun;
using Application.Interfaces.Eventos;

namespace Infrastructure.Mensajeria;

public sealed record EventBridgeOpciones(string Bus);

// En AWS los eventos van al bus de EventBridge compartido (creado fuera de este repo), que reemplaza a
// RabbitMQ. DetailType es el nombre del evento sin el sufijo "Event" (ReporteCreadoEvent -> ReporteCreado)
// y Detail es el evento en JSON con la misma convención que la API (camelCase, enums como códigos).
public class EventBridgeEventPublisher(IAmazonEventBridge eventBridge, EventBridgeOpciones opciones) : IEventPublisher
{
    public const string Source = "urban-alert.reportes";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(CodigoEnum.Politica, allowIntegerValues: false) }
    };

    public async Task PublicarAsync(object evento, CancellationToken cancellationToken)
    {
        Type tipo = evento.GetType();
        PutEventsRequest solicitud = new()
        {
            Entries =
            [
                new PutEventsRequestEntry
                {
                    EventBusName = opciones.Bus,
                    Source = Source,
                    DetailType = TipoDeDetalle(tipo),
                    Detail = JsonSerializer.Serialize(evento, tipo, OpcionesJson)
                }
            ]
        };

        PutEventsResponse respuesta = await eventBridge.PutEventsAsync(solicitud, cancellationToken);

        // PutEvents responde 200 aunque rechace entradas: el fallo viene en cada entrada.
        if (respuesta.FailedEntryCount is > 0)
        {
            PutEventsResultEntry? fallida = respuesta.Entries?.FirstOrDefault(e => e.ErrorCode is not null);
            throw new InvalidOperationException(
                $"EventBridge rechazó el evento {solicitud.Entries[0].DetailType}: {fallida?.ErrorCode} {fallida?.ErrorMessage}".TrimEnd());
        }
    }

    private static string TipoDeDetalle(Type tipo) =>
        tipo.Name.EndsWith("Event", StringComparison.Ordinal) ? tipo.Name[..^"Event".Length] : tipo.Name;
}
