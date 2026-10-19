using Microsoft.Extensions.Configuration;

namespace Infrastructure.Mensajeria.Outbox;

// Sección "Outbox" (spec 002 RF-002 a RF-004, RNF-001). Habilitado=false deja los eventos pendientes sin entregar
// (útil en pruebas que controlan el despachador a mano).
public sealed record OutboxOpciones(
    bool Habilitado = true,
    int IntervaloMs = 1000,
    int Lote = 20,
    int MaxIntentos = 10,
    int TimeoutPublicacionSegundos = 10)
{
    public static OutboxOpciones Desde(IConfiguration configuration)
    {
        IConfigurationSection seccion = configuration.GetSection("Outbox");
        OutboxOpciones opciones = new(
            seccion.GetValue("Habilitado", true),
            seccion.GetValue("IntervaloMs", 1000),
            seccion.GetValue("Lote", 20),
            seccion.GetValue("MaxIntentos", 10),
            seccion.GetValue("TimeoutPublicacionSegundos", 10));

        if (opciones.IntervaloMs < 1 || opciones.Lote < 1 || opciones.MaxIntentos < 1 || opciones.TimeoutPublicacionSegundos < 1)
            throw new InvalidOperationException("Outbox: IntervaloMs, Lote, MaxIntentos y TimeoutPublicacionSegundos deben ser mayores que cero.");

        return opciones;
    }
}
