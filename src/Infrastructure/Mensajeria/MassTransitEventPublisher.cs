using Application.Interfaces.Eventos;
using MassTransit;

namespace Infrastructure.Mensajeria;

public class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
{
    public Task PublicarAsync<TEvento>(TEvento evento, CancellationToken cancellationToken) where TEvento : class =>
        publishEndpoint.Publish(evento, cancellationToken);
}
