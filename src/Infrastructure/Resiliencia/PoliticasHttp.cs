using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Infrastructure.Resiliencia;

// Sección "Resiliencia:<Geoespacial|Multimedia>" (spec 002 RF-007 a RF-012). Los valores por defecto son los de la spec.
public sealed record ResilienciaOpciones(
    int Reintentos = 2,
    int RetardoBaseMs = 200,
    double RatioFallos = 0.5,
    int VentanaSegundos = 30,
    int MinimoLlamadas = 5,
    int PausaSegundos = 15,
    int TimeoutTotalSegundos = 12)
{
    public static ResilienciaOpciones Desde(IConfiguration configuration, string dependencia)
    {
        IConfigurationSection seccion = configuration.GetSection($"Resiliencia:{dependencia}");
        ResilienciaOpciones porDefecto = new();
        return new ResilienciaOpciones(
            seccion.GetValue("Reintentos", porDefecto.Reintentos),
            seccion.GetValue("RetardoBaseMs", porDefecto.RetardoBaseMs),
            seccion.GetValue("RatioFallos", porDefecto.RatioFallos),
            seccion.GetValue("VentanaSegundos", porDefecto.VentanaSegundos),
            seccion.GetValue("MinimoLlamadas", porDefecto.MinimoLlamadas),
            seccion.GetValue("PausaSegundos", porDefecto.PausaSegundos),
            seccion.GetValue("TimeoutTotalSegundos", porDefecto.TimeoutTotalSegundos));
    }
}

// Pipelines de las llamadas síncronas de la creación (plan D-004 a D-006): Retry (exponencial con jitter) →
// CircuitBreaker → Timeout por intento. Se registran antes de cualquier otro handler del cliente, así que
// handlers añadidos después (la firma SigV4 de Geoespacial) se ejecutan de nuevo en cada intento.
public static class PoliticasHttp
{
    public const string Geoespacial = "geoespacial";
    public const string Multimedia = "multimedia";

    // Geoespacial es idempotente por reporte (200 ante una repetición idéntica): se reintentan todos los
    // fallos transitorios (sin conexión, timeout, 408, 429, 5xx). 400/404/409 no se reintentan (RF-008).
    public static IHttpClientBuilder AgregarResilienciaGeoespacial(
        this IHttpClientBuilder cliente, ResilienciaOpciones opciones, TimeSpan timeoutPorIntento) =>
        cliente.AgregarPipeline(Geoespacial, opciones, timeoutPorIntento, soloSinConexion: false);

    // La subida a Multimedia no es idempotente: solo se reintenta si la conexión ni siquiera se estableció
    // (la imagen nunca se envió). Un timeout o un 5xx no se reintentan, pero sí cuentan para el circuito (RF-007).
    public static IHttpClientBuilder AgregarResilienciaMultimedia(
        this IHttpClientBuilder cliente, ResilienciaOpciones opciones, TimeSpan timeoutPorIntento) =>
        cliente.AgregarPipeline(Multimedia, opciones, timeoutPorIntento, soloSinConexion: true);

    public static bool EsFalloDeConexion(Exception? excepcion) =>
        excepcion is HttpRequestException { HttpRequestError: HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError };

    private static IHttpClientBuilder AgregarPipeline(
        this IHttpClientBuilder cliente, string nombre, ResilienciaOpciones opciones, TimeSpan timeoutPorIntento, bool soloSinConexion)
    {
        cliente.AddResilienceHandler(nombre, pipeline =>
        {
            HttpRetryStrategyOptions reintento = new()
            {
                MaxRetryAttempts = opciones.Reintentos,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(opciones.RetardoBaseMs),
                // Un Retry-After largo del servidor rompería el presupuesto de tiempo de la creación (RNF-003).
                ShouldRetryAfterHeader = false
            };
            if (soloSinConexion)
                reintento.ShouldHandle = argumentos => ValueTask.FromResult(EsFalloDeConexion(argumentos.Outcome.Exception));

            pipeline.AddRetry(reintento);
            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = opciones.RatioFallos,
                SamplingDuration = TimeSpan.FromSeconds(opciones.VentanaSegundos),
                MinimumThroughput = opciones.MinimoLlamadas,
                BreakDuration = TimeSpan.FromSeconds(opciones.PausaSegundos)
            });
            pipeline.AddTimeout(timeoutPorIntento);
        });

        return cliente;
    }
}
