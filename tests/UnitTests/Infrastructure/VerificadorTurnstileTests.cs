using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Application.Captcha;
using Application.Interfaces.Captcha;
using Infrastructure;
using Infrastructure.Captcha;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UnitTests.Infrastructure;

// Spec 003 RF-003 a RF-009: verificador real de AddInfrastructure contra un Turnstile falso.
public class VerificadorTurnstileTests
{
    private const string ClaveSecreta = "1x0000000000000000000000000000000AA";
    private const string Token = "XXXX.DUMMY.TOKEN.XXXX";

    private readonly TurnstileFalso _turnstile = new();
    private readonly LogsCapturados _logs = new();

    private ServiceProvider Proveedor(Dictionary<string, string?>? extra = null)
    {
        Dictionary<string, string?> valores = new()
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=x;Username=x;Password=x",
            ["Geoespacial:BaseUrl"] = "http://geo",
            ["Multimedia:BaseUrl"] = "http://mm",
            ["RabbitMq:Host"] = "localhost",
            ["Outbox:Habilitado"] = "false",
            ["Captcha:Habilitado"] = "true",
            ["Captcha:ClaveSecreta"] = ClaveSecreta,
            ["Captcha:Accion"] = "crear-reporte",
            ["Captcha:HostnamesPermitidos:0"] = "dfa8690dowmry.cloudfront.net",
            ["Resiliencia:Captcha:RetardoBaseMs"] = "1"
        };
        foreach ((string clave, string? valor) in extra ?? [])
            valores[clave] = valor;

        ServiceCollection servicios = new();
        servicios.AddLogging(logs => logs.AddProvider(_logs));
        servicios.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(valores).Build());
        servicios.ConfigureHttpClientDefaults(cliente => cliente.ConfigurePrimaryHttpMessageHandler(() => _turnstile));
        return servicios.BuildServiceProvider();
    }

    private static Task VerificarAsync(ServiceProvider proveedor, string? token = Token) =>
        proveedor.GetRequiredService<IVerificadorCaptcha>().VerificarAsync(token, CancellationToken.None);

    private static Func<HttpResponseMessage> Respuesta(bool success, string hostname = "dfa8690dowmry.cloudfront.net",
        string action = "crear-reporte", params string[] errores) => () => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new Dictionary<string, object>
        {
            ["success"] = success,
            ["hostname"] = hostname,
            ["action"] = action,
            ["error-codes"] = errores,
            ["challenge_ts"] = "2026-10-19T12:00:00Z"
        })
    };

    [Fact]
    public async Task UnaPruebaValidaDeEsteSitioYAccion_SeAcepta_YEnviaSecretYResponseComoFormulario()
    {
        _turnstile.Responder(Respuesta(true));
        using ServiceProvider proveedor = Proveedor();

        await VerificarAsync(proveedor);

        (Uri? uri, string cuerpo) = Assert.Single(_turnstile.Peticiones);
        Assert.Equal(new Uri(CaptchaOpciones.UrlTurnstile), uri);
        Assert.Contains($"secret={ClaveSecreta}", cuerpo);
        Assert.Contains("response=XXXX.DUMMY.TOKEN.XXXX", cuerpo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SinPrueba_SeRechazaSinLlamarAlProveedor(string? token)
    {
        using ServiceProvider proveedor = Proveedor();

        CaptchaInvalidoException error = await Assert.ThrowsAsync<CaptchaInvalidoException>(() => VerificarAsync(proveedor, token));

        Assert.Equal("missing-input-response", error.Motivo);
        Assert.Empty(_turnstile.Peticiones);
    }

    [Fact]
    public async Task UnaPruebaDemasiadoLarga_SeRechazaSinLlamarAlProveedor()
    {
        using ServiceProvider proveedor = Proveedor();

        await Assert.ThrowsAsync<CaptchaInvalidoException>(() => VerificarAsync(proveedor, new string('a', VerificadorTurnstile.LongitudMaxima + 1)));
        Assert.Empty(_turnstile.Peticiones);
    }

    [Theory]
    [InlineData("invalid-input-response")]
    [InlineData("timeout-or-duplicate")]
    public async Task UnaPruebaInvalidaVencidaOYaUsada_SeRechaza(string codigo)
    {
        _turnstile.Responder(Respuesta(false, errores: codigo));
        using ServiceProvider proveedor = Proveedor();

        CaptchaInvalidoException error = await Assert.ThrowsAsync<CaptchaInvalidoException>(() => VerificarAsync(proveedor));
        Assert.Equal(codigo, error.Motivo);
    }

    [Fact]
    public async Task UnaPruebaDeOtroDominio_SeRechaza()
    {
        _turnstile.Responder(Respuesta(true, hostname: "sitio-ajeno.example"));
        using ServiceProvider proveedor = Proveedor();

        CaptchaInvalidoException error = await Assert.ThrowsAsync<CaptchaInvalidoException>(() => VerificarAsync(proveedor));
        Assert.StartsWith("hostname-no-permitido", error.Motivo);
    }

    [Fact]
    public async Task UnaPruebaDeOtraAccion_SeRechaza()
    {
        _turnstile.Responder(Respuesta(true, action: "login"));
        using ServiceProvider proveedor = Proveedor();

        CaptchaInvalidoException error = await Assert.ThrowsAsync<CaptchaInvalidoException>(() => VerificarAsync(proveedor));
        Assert.StartsWith("accion-no-esperada", error.Motivo);
    }

    [Fact]
    public async Task SinDominiosNiAccionConfigurados_NoSeComprueban()
    {
        _turnstile.Responder(Respuesta(true, hostname: "example.com", action: ""));
        using ServiceProvider proveedor = Proveedor(new() { ["Captcha:HostnamesPermitidos:0"] = null, ["Captcha:Accion"] = "" });

        await VerificarAsync(proveedor);
    }

    [Theory]
    [InlineData("invalid-input-secret")]
    [InlineData("internal-error")]
    public async Task UnErrorDelServicioODeConfiguracion_EsNoDisponible(string codigo)
    {
        _turnstile.Responder(Respuesta(false, errores: codigo));
        using ServiceProvider proveedor = Proveedor();

        await Assert.ThrowsAsync<CaptchaNoDisponibleException>(() => VerificarAsync(proveedor));
    }

    [Fact]
    public async Task UnErrorHttpDelProveedor_EsNoDisponible_YNoSeReintenta()
    {
        _turnstile.Responder(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using ServiceProvider proveedor = Proveedor();

        await Assert.ThrowsAsync<CaptchaNoDisponibleException>(() => VerificarAsync(proveedor));
        Assert.Single(_turnstile.Peticiones);
    }

    [Fact]
    public async Task UnaRespuestaIlegible_EsNoDisponible()
    {
        _turnstile.Responder(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>") });
        using ServiceProvider proveedor = Proveedor();

        await Assert.ThrowsAsync<CaptchaNoDisponibleException>(() => VerificarAsync(proveedor));
    }

    [Fact]
    public async Task SinConexion_SeReintentaYSiPersiste_EsNoDisponible()
    {
        _turnstile.Responder(() => throw new HttpRequestException(HttpRequestError.ConnectionError, "Connection refused"));
        using ServiceProvider proveedor = Proveedor();

        await Assert.ThrowsAsync<CaptchaNoDisponibleException>(() => VerificarAsync(proveedor));
        Assert.Equal(3, _turnstile.Peticiones.Count);
    }

    [Fact]
    public async Task ConElProveedorCaido_ElCircuitoAbre_YFallaRapido()
    {
        _turnstile.Responder(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using ServiceProvider proveedor = Proveedor();
        for (int i = 0; i < 5; i++)
            await Assert.ThrowsAsync<CaptchaNoDisponibleException>(() => VerificarAsync(proveedor));

        Stopwatch reloj = Stopwatch.StartNew();
        CaptchaNoDisponibleException error = await Assert.ThrowsAsync<CaptchaNoDisponibleException>(() => VerificarAsync(proveedor));
        reloj.Stop();

        Assert.Contains("circuito abierto", error.Message);
        Assert.Equal(5, _turnstile.Peticiones.Count);
        Assert.True(reloj.ElapsedMilliseconds < 50, $"Con el circuito abierto tardó {reloj.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task LosRechazosQuedanEnElLog_SinLaPruebaNiLaClaveSecreta()
    {
        _turnstile.Responder(Respuesta(false, errores: "invalid-input-response"));
        using ServiceProvider proveedor = Proveedor();

        await Assert.ThrowsAsync<CaptchaInvalidoException>(() => VerificarAsync(proveedor));

        Assert.Contains(_logs.Mensajes, m => m.Contains("Verificación humana rechazada: invalid-input-response"));
        Assert.DoesNotContain(_logs.Mensajes, m => m.Contains(Token) || m.Contains(ClaveSecreta));
    }

    [Fact]
    public void ActivadoSinClaveSecreta_LaConfiguracionNoArranca()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => Proveedor(new() { ["Captcha:ClaveSecreta"] = "" }));
        Assert.Contains("urban-alert-reportes-captcha", error.Message);
    }

    [Fact]
    public async Task Desactivado_NoLlamaAlProveedorNiExigePrueba()
    {
        using ServiceProvider proveedor = Proveedor(new() { ["Captcha:Habilitado"] = "false", ["Captcha:ClaveSecreta"] = "" });

        Assert.IsType<VerificadorDesactivado>(proveedor.GetRequiredService<IVerificadorCaptcha>());
        await VerificarAsync(proveedor, null);
        Assert.Empty(_turnstile.Peticiones);
    }

    private sealed class TurnstileFalso : HttpMessageHandler
    {
        private Func<HttpResponseMessage> _respuesta = () => new HttpResponseMessage(HttpStatusCode.OK);

        public ConcurrentQueue<(Uri? Uri, string Cuerpo)> PeticionesCola { get; } = new();

        public IReadOnlyList<(Uri? Uri, string Cuerpo)> Peticiones => PeticionesCola.ToArray();

        public void Responder(Func<HttpResponseMessage> respuesta) => _respuesta = respuesta;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string cuerpo = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            PeticionesCola.Enqueue((request.RequestUri, cuerpo));
            return _respuesta();
        }
    }

    private sealed class LogsCapturados : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _mensajes = new();

        public IReadOnlyList<string> Mensajes => _mensajes.ToArray();

        public ILogger CreateLogger(string categoryName) => new Registrador(_mensajes);

        public void Dispose()
        {
        }

        private sealed class Registrador(ConcurrentQueue<string> mensajes) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                mensajes.Enqueue(formatter(state, exception) + (exception is null ? "" : " " + exception));
        }
    }
}
