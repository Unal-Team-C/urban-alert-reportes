using System.Net;
using System.Net.Http.Json;
using Amazon.Runtime;
using Application.Interfaces.Eventos;
using Application.Interfaces.Geoespacial;
using AwsSignatureVersion4;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Infrastructure;

// En la nube, Reportes llama a la ruta interna de Geoespacial por el API Gateway, que exige firma IAM (SigV4).
public class FirmaGeoespacialTests
{
    private static readonly SessionAWSCredentials Credenciales = new("AKIDEXAMPLE", "wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY", "token-de-sesion");

    [Fact]
    public async Task AwsSignatureHandler_FirmaLaPeticionParaExecuteApi_ConCredencialesDeSesion()
    {
        ManejadorCaptura captura = new();
        AwsSignatureHandler firma = new(new AwsSignatureHandlerSettings("us-east-2", "execute-api", Credenciales))
        {
            InnerHandler = captura
        };
        using HttpClient cliente = new(firma) { BaseAddress = new Uri("https://abc123.execute-api.us-east-2.amazonaws.com/") };

        await cliente.PostAsJsonAsync("api/v1/geoespacial/coordinates", new { reportId = Guid.Empty });

        Dictionary<string, string> cabeceras = Assert.Single(captura.Cabeceras);
        Assert.StartsWith("AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/", cabeceras["Authorization"]);
        Assert.Contains("/us-east-2/execute-api/aws4_request", cabeceras["Authorization"]);
        Assert.Equal("token-de-sesion", cabeceras["X-Amz-Security-Token"]);
    }

    private const string Gateway = "https://abc123.execute-api.us-east-2.amazonaws.com";

    // Configuración mínima de AddInfrastructure; la cadena de conexión no se usa (no se abre la base).
    private static IGeoespacialClient ClienteDesdeDi(ManejadorCaptura captura, Dictionary<string, string?> extra) =>
        ProveedorDesdeDi(captura, extra, conCredenciales: true).GetRequiredService<IGeoespacialClient>();

    private static ServiceProvider ProveedorDesdeDi(ManejadorCaptura captura, Dictionary<string, string?> extra, bool conCredenciales)
    {
        Dictionary<string, string?> valores = new()
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=x;Username=x;Password=x",
            ["Geoespacial:BaseUrl"] = Gateway,
            ["Multimedia:BaseUrl"] = Gateway,
            ["RabbitMq:Host"] = "localhost",
            ["Mensajeria:Transporte"] = "EventBridge",
            ["EventBridge:Bus"] = "arn:aws:events:us-east-2:348364742283:event-bus/urban-alert-bus-eventos"
        };
        foreach ((string clave, string? valor) in extra)
            valores[clave] = valor;

        IConfiguration configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        ServiceCollection servicios = new();
        servicios.AddLogging();
        if (conCredenciales)
            servicios.AddSingleton<AWSCredentials>(Credenciales);
        servicios.AddInfrastructure(configuracion);
        servicios.ConfigureHttpClientDefaults(cliente => cliente.ConfigurePrimaryHttpMessageHandler(() => captura));

        return servicios.BuildServiceProvider();
    }

    [Fact]
    public void ClienteGeoespacial_ConFirmaIam_SeCreaSinCredencialesDisponibles()
    {
        // Las credenciales se resuelven en la primera petición firmada, no al crear los clientes: sin ellas,
        // los controladores (que reciben todos los handlers) se siguen creando y las lecturas funcionan.
        using ServiceProvider proveedor = ProveedorDesdeDi(new ManejadorCaptura(), new()
        {
            ["Geoespacial:FirmaIam:Region"] = "us-east-2",
            ["AWS_REGION"] = "us-east-2"
        }, conCredenciales: false);
        using IServiceScope scope = proveedor.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGeoespacialClient>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEventPublisher>());
    }

    [Fact]
    public async Task ClienteGeoespacial_ConFirmaIam_FirmaYUsaLaRutaDelGateway()
    {
        ManejadorCaptura captura = new();
        IGeoespacialClient cliente = ClienteDesdeDi(captura, new()
        {
            ["Geoespacial:RutaCoordenadas"] = "api/v1/geoespacial/coordinates",
            ["Geoespacial:FirmaIam:Region"] = "us-east-2"
        });

        Guid idCoordenada = await cliente.AsignarCoordenadaAsync(Guid.NewGuid(), 4.65, -74.05, CancellationToken.None);

        Assert.Equal(Guid.Parse("0199d2f1-0000-7000-8000-0000000000c0"), idCoordenada);
        Assert.Equal(new Uri($"{Gateway}/api/v1/geoespacial/coordinates"), Assert.Single(captura.Uris));
        Dictionary<string, string> cabeceras = Assert.Single(captura.Cabeceras);
        Assert.StartsWith("AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/", cabeceras["Authorization"]);
        Assert.Contains("/us-east-2/execute-api/aws4_request", cabeceras["Authorization"]);
    }

    [Fact]
    public async Task ClienteGeoespacial_SinFirmaIam_NoFirmaYUsaLaRutaDelServicio()
    {
        ManejadorCaptura captura = new();
        IGeoespacialClient cliente = ClienteDesdeDi(captura, new());

        await cliente.AsignarCoordenadaAsync(Guid.NewGuid(), 4.65, -74.05, CancellationToken.None);

        Assert.Equal(new Uri($"{Gateway}/api/v1/geospatial/coordinates"), Assert.Single(captura.Uris));
        Assert.False(Assert.Single(captura.Cabeceras).ContainsKey("Authorization"));
    }

    private sealed class ManejadorCaptura : HttpMessageHandler
    {
        // El manejador de firma quita sus cabeceras al terminar el envío: se copian en el momento.
        public List<Dictionary<string, string>> Cabeceras { get; } = [];

        public List<Uri?> Uris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Cabeceras.Add(request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase));
            Uris.Add(request.RequestUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(new
                {
                    coordinateId = Guid.Parse("0199d2f1-0000-7000-8000-0000000000c0"),
                    reportId = Guid.Empty,
                    coordinate = new { lat = 4.65, lon = -74.05 }
                })
            });
        }
    }
}
