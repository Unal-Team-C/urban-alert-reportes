using Application.Interfaces.Eventos;

namespace Infrastructure.Mensajeria.Outbox;

public enum EstadoMensajeOutbox
{
    Pendiente,
    Entregado,
    Fallido
}

// Evento de dominio por entregar al bus (spec 002 §7). Se guarda en la misma transacción que el reporte
// y lo entrega DespachadorOutbox; nunca se borra (ENTREGADO y FALLIDO quedan como registro).
public class MensajeOutbox
{
    public const int TipoMaxLength = 200;
    public const int UltimoErrorMaxLength = 2000;

    private static readonly TimeSpan EsperaMaxima = TimeSpan.FromMinutes(5);

    private MensajeOutbox() { }

    public Guid Id { get; private set; }
    public string Tipo { get; private set; } = "";
    public string Contenido { get; private set; } = "";
    public DateTime RegistradoEn { get; private set; }
    public int Intentos { get; private set; }
    public DateTime ProximoIntento { get; private set; }
    public DateTime? EntregadoEn { get; private set; }
    public EstadoMensajeOutbox Estado { get; private set; }
    public string? UltimoError { get; private set; }

    public static MensajeOutbox Crear(IEventoDominio evento, DateTime ahora) => new()
    {
        Id = evento.IdEvento,
        Tipo = SerializacionEventos.NombreDeTipo(evento),
        Contenido = SerializacionEventos.Serializar(evento),
        RegistradoEn = ahora,
        ProximoIntento = ahora,
        Estado = EstadoMensajeOutbox.Pendiente
    };

    public void MarcarEntregado(DateTime ahora)
    {
        Estado = EstadoMensajeOutbox.Entregado;
        EntregadoEn = ahora;
        UltimoError = null;
    }

    // Espera min(2^intentos s, 5 min) antes del siguiente intento; tras maxIntentos queda FALLIDO (RF-003, RF-004).
    public void RegistrarFallo(string error, DateTime ahora, int maxIntentos)
    {
        Intentos++;
        UltimoError = error.Length > UltimoErrorMaxLength ? error[..UltimoErrorMaxLength] : error;

        if (Intentos >= maxIntentos)
        {
            Estado = EstadoMensajeOutbox.Fallido;
            return;
        }

        TimeSpan espera = TimeSpan.FromSeconds(Math.Pow(2, Intentos));
        ProximoIntento = ahora + (espera < EsperaMaxima ? espera : EsperaMaxima);
    }
}
