using Application.Interfaces.Eventos;
using MassTransit;

namespace Infrastructure.Mensajeria;

public class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
{
    public Task PublicarAsync(object evento, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(evento, evento.GetType(), cancellationToken);
}
