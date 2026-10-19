using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Comun;
using Application.Interfaces.Eventos;
using Application.Reportes.Eventos;

namespace Infrastructure.Mensajeria.Outbox;

// Misma convención que la API y el bus (camelCase, enums como códigos): lo que se guarda en el outbox se
// vuelve a leer sin pérdida y se publica igual que si se hubiera publicado directamente (spec 002 RF-006).
public static class SerializacionEventos
{
    public static JsonSerializerOptions Opciones { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(CodigoEnum.Politica, allowIntegerValues: false) }
    };

    // Solo los eventos registrados aquí se pueden leer del outbox: el tipo guardado no se resuelve por reflexión.
    private static readonly Dictionary<string, Type> TiposConocidos = new[] { typeof(ReporteCreadoEvent) }
        .ToDictionary(tipo => tipo.Name, StringComparer.Ordinal);

    public static string NombreDeTipo(IEventoDominio evento)
    {
        string nombre = evento.GetType().Name;
        if (!TiposConocidos.ContainsKey(nombre))
            throw new InvalidOperationException($"El evento {nombre} no está registrado en SerializacionEventos.");
        return nombre;
    }

    public static string Serializar(IEventoDominio evento) =>
        JsonSerializer.Serialize(evento, evento.GetType(), Opciones);

    public static object Deserializar(string tipo, string contenido)
    {
        if (!TiposConocidos.TryGetValue(tipo, out Type? clr))
            throw new InvalidOperationException($"Tipo de evento desconocido en el outbox: {tipo}.");

        return JsonSerializer.Deserialize(contenido, clr, Opciones)
               ?? throw new InvalidOperationException($"El contenido del evento {tipo} está vacío.");
    }
}
