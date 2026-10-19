namespace Application.Interfaces.Eventos;

// No genérico: el despachador del outbox publica eventos deserializados como object, y el tipo que viaja
// (DetailType en EventBridge, tipo de mensaje en MassTransit) es el del evento en tiempo de ejecución.
public interface IEventPublisher
{
    Task PublicarAsync(object evento, CancellationToken cancellationToken);
}
