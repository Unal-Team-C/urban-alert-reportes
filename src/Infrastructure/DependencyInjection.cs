using System.Text.Json.Serialization;
using Amazon;
using Amazon.EventBridge;
using Amazon.Runtime;
using AwsSignatureVersion4;
using Infrastructure.Aws;
using Application.Comun;
using Application.Interfaces.Eventos;
using Application.Interfaces.Geoespacial;
using Application.Interfaces.Multimedia;
using Application.Interfaces.Reportes;
using Infrastructure.Geoespacial;
using Infrastructure.Mensajeria;
using Infrastructure.Multimedia;
using Infrastructure.Persistencia;
using Infrastructure.Persistencia.Repositorios;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Postgres")
                                  ?? throw new InvalidOperationException("No se encontró la cadena de conexión 'Postgres'.");

        services.AddDbContext<ReportesDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IReporteRepository, ReporteRepository>();

        AgregarGeoespacial(services, configuration);
        AgregarMultimedia(services, configuration);
        AgregarMensajeria(services, configuration);

        return services;
    }

    private static void AgregarGeoespacial(IServiceCollection services, IConfiguration configuration)
    {
        string? baseUrl = configuration["Geoespacial:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("No se encontró la configuración 'Geoespacial:BaseUrl'.");

        // Timeout corto: la llamada es síncrona dentro de la creación del reporte.
        int timeoutSegundos = configuration.GetValue<int?>("Geoespacial:TimeoutSegundos") ?? 3;

        string ruta = configuration["Geoespacial:RutaCoordenadas"] is { Length: > 0 } r ? r.TrimStart('/') : GeoespacialOpciones.RutaPorDefecto;
        services.AddSingleton(new GeoespacialOpciones(ruta));

        IHttpClientBuilder cliente = services.AddHttpClient<IGeoespacialClient, GeoespacialHttpClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(timeoutSegundos);
        });

        // En AWS la ruta de asignación del API Gateway exige firma IAM (SigV4) con el rol de tarea.
        string? regionFirma = configuration["Geoespacial:FirmaIam:Region"];
        if (!string.IsNullOrWhiteSpace(regionFirma))
        {
            CredencialesPerezosas credencialesPorDefecto = new();
            cliente.AddHttpMessageHandler(sp => new AwsSignatureHandler(new AwsSignatureHandlerSettings(
                regionFirma,
                "execute-api",
                sp.GetService<AWSCredentials>() ?? credencialesPorDefecto)));
        }
    }

    private static void AgregarMultimedia(IServiceCollection services, IConfiguration configuration)
    {
        string? baseUrl = configuration["Multimedia:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("No se encontró la configuración 'Multimedia:BaseUrl'.");

        // Timeout más alto que Geoespacial: incluye subir el archivo, no solo una consulta.
        int timeoutSegundos = configuration.GetValue<int?>("Multimedia:TimeoutSegundos") ?? 10;

        string modo = configuration["Multimedia:Modo"] ?? nameof(ModoMultimedia.Emulador);
        if (!Enum.TryParse(modo, ignoreCase: true, out ModoMultimedia modoMultimedia))
            throw new InvalidOperationException($"'Multimedia:Modo' no admite '{modo}' (Emulador | Gateway).");
        services.AddSingleton(new MultimediaOpciones(modoMultimedia));

        services.AddHttpClient<IMultimediaClient, MultimediaHttpClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(timeoutSegundos);
        });
    }

    // RabbitMq (local, por defecto) o EventBridge (AWS). Con EventBridge no se registra MassTransit.
    private static void AgregarMensajeria(IServiceCollection services, IConfiguration configuration)
    {
        string transporte = configuration["Mensajeria:Transporte"] ?? "RabbitMq";

        if (string.Equals(transporte, "EventBridge", StringComparison.OrdinalIgnoreCase))
            AgregarEventBridge(services, configuration);
        else if (string.Equals(transporte, "RabbitMq", StringComparison.OrdinalIgnoreCase))
            AgregarMassTransit(services, configuration);
        else
            throw new InvalidOperationException($"'Mensajeria:Transporte' no admite '{transporte}' (RabbitMq | EventBridge).");
    }

    private static void AgregarEventBridge(IServiceCollection services, IConfiguration configuration)
    {
        string? bus = configuration["EventBridge:Bus"];
        if (string.IsNullOrWhiteSpace(bus))
            throw new InvalidOperationException("No se encontró la configuración 'EventBridge:Bus'.");

        // Región de AWS_REGION (variable de entorno o configuración); credenciales de la cadena por defecto
        // del SDK (rol de tarea en ECS), salvo que haya unas registradas (pruebas).
        string? region = configuration["AWS_REGION"];
        // Timeout corto: el despachador del outbox publica dentro de una transacción por lote (plan D-007).
        AmazonEventBridgeConfig configEventBridge = new() { Timeout = TimeSpan.FromSeconds(5), MaxErrorRetry = 2 };
        if (!string.IsNullOrWhiteSpace(region))
            configEventBridge.RegionEndpoint = RegionEndpoint.GetBySystemName(region);

        services.AddSingleton(new EventBridgeOpciones(bus));
        services.AddSingleton<IAmazonEventBridge>(sp => sp.GetService<AWSCredentials>() is { } credenciales
            ? new AmazonEventBridgeClient(credenciales, configEventBridge)
            : new AmazonEventBridgeClient(configEventBridge));
        services.AddScoped<IEventPublisher, EventBridgeEventPublisher>();
    }

    private static void AgregarMassTransit(IServiceCollection services, IConfiguration configuration)
    {
        string host = configuration["RabbitMq:Host"]
                      ?? throw new InvalidOperationException("No se encontró la configuración 'RabbitMq:Host'.");
        string virtualHost = configuration["RabbitMq:VirtualHost"] ?? "/";
        string usuario = configuration["RabbitMq:Username"] ?? "guest";
        string contrasena = configuration["RabbitMq:Password"] ?? "guest";

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.UsingRabbitMq((_, cfg) =>
            {
                cfg.Host(host, virtualHost, h =>
                {
                    h.Username(usuario);
                    h.Password(contrasena);
                });

                // Los eventos usan la misma convención que la API: enums como códigos
                // UPPER_SNAKE_CASE ("REPORTADO"); sin esto MassTransit los envía como números.
                cfg.ConfigureJsonSerializerOptions(opciones =>
                {
                    opciones.Converters.Insert(0, new JsonStringEnumConverter(CodigoEnum.Politica, allowIntegerValues: false));
                    return opciones;
                });
            });
        });

        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
    }
}
