using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Amazon.Runtime;
using Application.Geoespacial;
using Application.Interfaces.Geoespacial;
using Application.Interfaces.Multimedia;
using Application.Multimedia;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UnitTests.Infrastructure;

// Spec 002 RF-007 a RF-012 y RNF-002: pipelines reales de AddInfrastructure con un servidor falso.
public class PoliticasHttpTests
{
    private const string Gateway = "https://abc123.execute-api.us-east-2.amazonaws.com";

    private readonly ServidorFalso _servidor = new();
    private readonly RelojFalso _reloj = new();
    private readonly LogsCapturados _logs = new();

    private ServiceProvider Proveedor(Dictionary<string, string?>? extra = null)
    {
        Dictionary<string, string?> valores = new()
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=x;Username=x;Password=x",
            ["Geoespacial:BaseUrl"] = Gateway,
            ["Multimedia:BaseUrl"] = Gateway,
            ["Multimedia:Modo"] = "Gateway",
            ["RabbitMq:Host"] = "localhost",
            ["Outbox:Habilitado"] = "false",
            // Esperas mínimas entre reintentos para que las pruebas sean rápidas.
            ["Resiliencia:Geoespacial:RetardoBaseMs"] = "1",
            ["Resiliencia:Multimedia:RetardoBaseMs"] = "1"
        };
        foreach ((string clave, string? valor) in extra ?? [])
            valores[clave] = valor;

        ServiceCollection servicios = new();
        servicios.AddLogging(logs => logs.AddProvider(_logs));
        servicios.AddSingleton<TimeProvider>(_reloj);
        servicios.AddSingleton<AWSCredentials>(new BasicAWSCredentials("AKIDEXAMPLE", "secreto"));
        servicios.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(valores).Build());
        servicios.ConfigureHttpClientDefaults(cliente => cliente.ConfigurePrimaryHttpMessageHandler(() => _servidor));
        return servicios.BuildServiceProvider();
    }

    private static Task<Guid> AsignarAsync(ServiceProvider proveedor) =>
        proveedor.GetRequiredService<IGeoespacialClient>().AsignarCoordenadaAsync(Guid.NewGuid(), 4.65, -74.05, CancellationToken.None);

    private static Task<string> SubirAsync(ServiceProvider proveedor) =>
        proveedor.GetRequiredService<IMultimediaClient>().SubirImagenAsync(
            new MemoryStream([1, 2, 3]), 3, "foto.png", "image/png", 4.65, -74.05, CancellationToken.None);

    [Fact]
    public async Task Geoespacial_ReintentaUnFalloTransitorio_YLaCreacionTieneExito()
    {
        _servidor.Responder(Estado(HttpStatusCode.ServiceUnavailable), CoordenadaCreada());
        using ServiceProvider proveedor = Proveedor();

        Assert.NotEqual(Guid.Empty, await AsignarAsync(proveedor));
        Assert.Equal(2, _servidor.Llamadas);
    }

    [Fact]
    public async Task Geoespacial_AgotaLosDosReintentos_YRespondeNoDisponible()
    {
        _servidor.Responder(Estado(HttpStatusCode.BadGateway));
        using ServiceProvider proveedor = Proveedor();

        await Assert.ThrowsAsync<GeoespacialNoDisponibleException>(() => AsignarAsync(proveedor));
        Assert.Equal(3, _servidor.Llamadas);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, typeof(CoordenadaInvalidaException))]
    [InlineData(HttpStatusCode.Conflict, typeof(GeoespacialNoDisponibleException))]
    public async Task Geoespacial_NoReintentaErroresDeCliente(HttpStatusCode estado, Type excepcion)
    {
        _servidor.Responder(Estado(estado));
        using ServiceProvider proveedor = Proveedor();

        await Assert.ThrowsAsync(excepcion, () => AsignarAsync(proveedor));
        Assert.Equal(1, _servidor.Llamadas);
    }

    [Fact]
    public async Task Geoespacial_FirmaCadaIntentoDeNuevo()
    {
        _servidor.Responder(Estado(HttpStatusCode.ServiceUnavailable), CoordenadaCreada());
        using ServiceProvider proveedor = Proveedor(new() { ["Geoespacial:FirmaIam:Region"] = "us-east-2" });

        await AsignarAsync(proveedor);

        Assert.Equal(2, _servidor.Cabeceras.Count);
        Assert.All(_servidor.Cabeceras, c => Assert.StartsWith("AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/", c["Authorization"]));
    }

    [Fact]
    public async Task Geoespacial_AbreElCircuito_YFallaRapidoSinLlamar()
    {
        _servidor.Responder(Estado(HttpStatusCode.ServiceUnavailable));
        using ServiceProvider proveedor = ProveedorSinReintentos();

        for (int i = 0; i < 5; i++)
            await Assert.ThrowsAsync<GeoespacialNoDisponibleException>(() => AsignarAsync(proveedor));
        Assert.Equal(5, _servidor.Llamadas);

        Stopwatch reloj = Stopwatch.StartNew();
        GeoespacialNoDisponibleException error = await Assert.ThrowsAsync<GeoespacialNoDisponibleException>(() => AsignarAsync(proveedor));
        reloj.Stop();

        Assert.Contains("circuito abierto", error.Message);
        Assert.Equal(5, _servidor.Llamadas);
        Assert.True(reloj.ElapsedMilliseconds < 50, $"Con el circuito abierto tardó {reloj.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task Geoespacial_TrasLaPausaDejaPasarUnaPrueba_YCierraElCircuitoSiTieneExito()
    {
        _servidor.Responder(Estado(HttpStatusCode.ServiceUnavailable));
        using ServiceProvider proveedor = ProveedorSinReintentos();
        for (int i = 0; i < 5; i++)
            await Assert.ThrowsAsync<GeoespacialNoDisponibleException>(() => AsignarAsync(proveedor));

        // Antes de la pausa (15 s) sigue abierto.
        _reloj.Avanzar(TimeSpan.FromSeconds(10));
        await Assert.ThrowsAsync<GeoespacialNoDisponibleException>(() => AsignarAsync(proveedor));
        Assert.Equal(5, _servidor.Llamadas);

        _servidor.Responder(CoordenadaCreada());
        _reloj.Avanzar(TimeSpan.FromSeconds(6));
        Assert.NotEqual(Guid.Empty, await AsignarAsync(proveedor));
        Assert.NotEqual(Guid.Empty, await AsignarAsync(proveedor));
        // Responder reinicia el contador: la llamada de prueba (half-open) y la siguiente, ya con el circuito cerrado.
        Assert.Equal(2, _servidor.Llamadas);
    }

    [Fact]
    public async Task Multimedia_ReintentaSoloSiNoHuboConexion()
    {
        _servidor.Responder(
            (_, _) => throw new HttpRequestException(HttpRequestError.ConnectionError, "Connection refused"),
            ImagenSubida());
        using ServiceProvider proveedor = Proveedor();

        Assert.Equal("https://multimedia.urbanalert.com/imagen.png", await SubirAsync(proveedor));
        Assert.Equal(2, _servidor.Llamadas);
    }

    [Fact]
    public async Task Multimedia_NoReintentaUn5xx_ParaNoDuplicarLaImagen()
    {
        _servidor.Responder(Estado(HttpStatusCode.ServiceUnavailable), ImagenSubida());
        using ServiceProvider proveedor = Proveedor();

        await Assert.ThrowsAsync<MultimediaNoDisponibleException>(() => SubirAsync(proveedor));
        Assert.Equal(1, _servidor.Llamadas);
    }

    [Fact]
    public async Task Multimedia_NoReintentaUnTimeout_YUsaElTimeoutPorIntentoConfigurado()
    {
        _servidor.Responder(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        using ServiceProvider proveedor = Proveedor(new() { ["Multimedia:TimeoutSegundos"] = "1" });

        Stopwatch reloj = Stopwatch.StartNew();
        MultimediaNoDisponibleException error = await Assert.ThrowsAsync<MultimediaNoDisponibleException>(() => SubirAsync(proveedor));

        Assert.Contains("a tiempo", error.Message);
        Assert.Equal(1, _servidor.Llamadas);
        Assert.InRange(reloj.Elapsed, TimeSpan.FromSeconds(0.9), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ElCircuitoDeGeoespacialNoAfectaAMultimedia()
    {
        _servidor.Responder(Estado(HttpStatusCode.ServiceUnavailable));
        using ServiceProvider proveedor = ProveedorSinReintentos();
        for (int i = 0; i < 5; i++)
            await Assert.ThrowsAsync<GeoespacialNoDisponibleException>(() => AsignarAsync(proveedor));

        _servidor.Responder(ImagenSubida());
        Assert.Equal("https://multimedia.urbanalert.com/imagen.png", await SubirAsync(proveedor));
    }

    [Fact]
    public async Task LosReintentosYLaAperturaDelCircuitoQuedanEnElLog()
    {
        _servidor.Responder(Estado(HttpStatusCode.ServiceUnavailable));
        using ServiceProvider proveedor = Proveedor();

        // 2 creaciones × 3 intentos = 6 fallos: el circuito se abre (mínimo 5 llamadas).
        for (int i = 0; i < 2; i++)
            await Assert.ThrowsAsync<GeoespacialNoDisponibleException>(() => AsignarAsync(proveedor));

        Assert.Contains(_logs.Mensajes, m => m.Contains("OnRetry"));
        Assert.Contains(_logs.Mensajes, m => m.Contains("OnCircuitOpened"));
    }

    private ServiceProvider ProveedorSinReintentos() => Proveedor(new() { ["Resiliencia:Geoespacial:Reintentos"] = "0" });

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Estado(HttpStatusCode estado) =>
        (_, _) => Task.FromResult(new HttpResponseMessage(estado) { Content = JsonContent.Create(new { code = (int)estado, message = "falla" }) });

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> CoordenadaCreada() =>
        (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new { coordinateId = Guid.NewGuid(), reportId = Guid.Empty, coordinate = new { lat = 4.65, lon = -74.05 } })
        });

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> ImagenSubida() =>
        (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new { imageUrl = "https://multimedia.urbanalert.com/imagen.png" })
        });

    // Responde en orden; la última respuesta se repite.
    private sealed class ServidorFalso : HttpMessageHandler
    {
        private List<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _respuestas = [];
        private int _llamadas;

        public int Llamadas => _llamadas;

        // El manejador de firma quita sus cabeceras al terminar el envío: se copian en el momento.
        public List<Dictionary<string, string>> Cabeceras { get; } = [];

        public void Responder(params Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>[] respuestas)
        {
            _respuestas = [.. respuestas];
            _llamadas = 0;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int indice = Interlocked.Increment(ref _llamadas) - 1;
            lock (Cabeceras)
                Cabeceras.Add(request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase));
            return _respuestas[Math.Min(indice, _respuestas.Count - 1)](request, cancellationToken);
        }
    }

    private sealed class LogsCapturados : ILoggerProvider
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _mensajes = new();

        public IReadOnlyList<string> Mensajes => _mensajes.ToArray();

        public ILogger CreateLogger(string categoryName) => new Registrador(_mensajes);

        public void Dispose()
        {
        }

        private sealed class Registrador(System.Collections.Concurrent.ConcurrentQueue<string> mensajes) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                mensajes.Enqueue(formatter(state, exception));
        }
    }

    // Solo el "ahora" es controlable (la pausa del circuito); los temporizadores siguen siendo reales.
    private sealed class RelojFalso : TimeProvider
    {
        private DateTimeOffset _ahora = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _ahora;

        public void Avanzar(TimeSpan tiempo) => _ahora += tiempo;
    }
}
