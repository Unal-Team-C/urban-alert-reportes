namespace Application.Interfaces.Eventos;

// Evento de dominio que se guarda en el outbox: su identificador es la clave de la fila y el que usa
// Auditoría para descartar entregas repetidas.
public interface IEventoDominio
{
    Guid IdEvento { get; }
}
