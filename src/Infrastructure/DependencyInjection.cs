using System.Text.Json.Serialization;
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
        AgregarMassTransit(services, configuration);

        return services;
    }

    private static void AgregarGeoespacial(IServiceCollection services, IConfiguration configuration)
    {
        string? baseUrl = configuration["Geoespacial:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("No se encontró la configuración 'Geoespacial:BaseUrl'.");

        // Timeout corto: la llamada es síncrona dentro de la creación del reporte.
        int timeoutSegundos = configuration.GetValue<int?>("Geoespacial:TimeoutSegundos") ?? 3;

        services.AddHttpClient<IGeoespacialClient, GeoespacialHttpClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(timeoutSegundos);
        });
    }

    private static void AgregarMultimedia(IServiceCollection services, IConfiguration configuration)
    {
        string? baseUrl = configuration["Multimedia:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("No se encontró la configuración 'Multimedia:BaseUrl'.");

        // Timeout más alto que Geoespacial: incluye subir el archivo, no solo una consulta.
        int timeoutSegundos = configuration.GetValue<int?>("Multimedia:TimeoutSegundos") ?? 10;

        services.AddHttpClient<IMultimediaClient, MultimediaHttpClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(timeoutSegundos);
        });
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
