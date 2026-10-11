namespace Application.Interfaces.Eventos;

public interface IEventPublisher
{
    Task PublicarAsync<TEvento>(TEvento evento, CancellationToken cancellationToken) where TEvento : class;
}
