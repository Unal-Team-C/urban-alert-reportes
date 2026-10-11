using System.Net;
using Amazon.Runtime;
using Application.Interfaces.Eventos;
using Infrastructure.Mensajeria;
using Infrastructure.Multimedia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Configuracion;

// La configuración que reciben las tareas en AWS (infra/service): EventBridge en lugar de RabbitMQ,
// Multimedia y Geoespacial por el API Gateway, firma IAM. Sin RabbitMq:Host la aplicación debe arrancar.
public class ConfiguracionNubeTests : IClassFixture<ConfiguracionNubeTests.FabricaNube>
{
    private readonly FabricaNube _fabrica;

    public ConfiguracionNubeTests(FabricaNube fabrica) => _fabrica = fabrica;

    [Fact]
    public void PublicaLosEventosEnEventBridge()
    {
        using IServiceScope scope = _fabrica.Services.CreateScope();

        Assert.IsType<EventBridgeEventPublisher>(scope.ServiceProvider.GetRequiredService<IEventPublisher>());
        Assert.Equal(
            "arn:aws:events:us-east-2:348364742283:event-bus/urban-alert-bus-eventos",
            scope.ServiceProvider.GetRequiredService<EventBridgeOpciones>().Bus);
    }

    [Fact]
    public void SubeLasImagenesPorElGateway()
    {
        Assert.Equal(ModoMultimedia.Gateway, _fabrica.Services.GetRequiredService<MultimediaOpciones>().Modo);
    }

    [Fact]
    public async Task ArrancaYRespondeLaSalud()
    {
        HttpResponseMessage respuesta = await _fabrica.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    public sealed class FabricaNube : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // La cadena de conexión de las pruebas de integración (entorno o User Secrets de la API).
            IConfiguration pruebas = new ConfigurationBuilder()
                .AddUserSecrets<Program>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Postgres", pruebas.GetConnectionString("Postgres"));
            builder.UseSetting("RabbitMq:Host", "");
            builder.UseSetting("Mensajeria:Transporte", "EventBridge");
            builder.UseSetting("EventBridge:Bus", "arn:aws:events:us-east-2:348364742283:event-bus/urban-alert-bus-eventos");
            builder.UseSetting("Geoespacial:BaseUrl", "https://abc123.execute-api.us-east-2.amazonaws.com");
            builder.UseSetting("Geoespacial:RutaCoordenadas", "api/v1/geoespacial/coordinates");
            builder.UseSetting("Geoespacial:FirmaIam:Region", "us-east-2");
            builder.UseSetting("Multimedia:BaseUrl", "https://abc123.execute-api.us-east-2.amazonaws.com");
            builder.UseSetting("Multimedia:Modo", "Gateway");
            builder.UseSetting("AWS_REGION", "us-east-2");

            // En el runner no hay credenciales de AWS; en ECS las da el rol de tarea.
            builder.ConfigureTestServices(services =>
                services.AddSingleton<AWSCredentials>(new BasicAWSCredentials("AKIDEXAMPLE", "secreto")));
        }
    }
}
