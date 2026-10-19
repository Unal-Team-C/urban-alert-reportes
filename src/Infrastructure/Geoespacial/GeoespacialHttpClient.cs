using System.Net;
using System.Net.Http.Json;
using Application.Geoespacial;
using Application.Interfaces.Geoespacial;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Infrastructure.Geoespacial;

public sealed record GeoespacialOpciones(string RutaCoordenadas)
{
    // Ruta del servicio (local). En AWS se llama por el API Gateway: api/v1/geoespacial/coordinates.
    public const string RutaPorDefecto = "api/v1/geospatial/coordinates";
}

// Contrato de Geoespacial: POST /api/v1/geospatial/coordinates (JSON en camelCase).
// 201 = asignación nueva, 200 = repetición idéntica, 400 = coordenada inválida,
// 409 = el reporte ya tiene otra coordenada, 503 = base de datos no disponible.
public class GeoespacialHttpClient(
    HttpClient httpClient,
    ILogger<GeoespacialHttpClient> logger,
    GeoespacialOpciones? opciones = null) : IGeoespacialClient
{
    private readonly string _rutaCoordenadas = opciones?.RutaCoordenadas ?? GeoespacialOpciones.RutaPorDefecto;

    public async Task<Guid> AsignarCoordenadaAsync(
        Guid idReporte, double latitud, double longitud, CancellationToken cancellationToken)
    {
        AssignLocation solicitud = new(idReporte, new Coordinate(latitud, longitud));
        HttpResponseMessage respuesta;

        try
        {
            respuesta = await httpClient.PostAsJsonAsync(_rutaCoordenadas, solicitud, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new GeoespacialNoDisponibleException("No fue posible conectar con el servicio Geoespacial.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GeoespacialNoDisponibleException("El servicio Geoespacial no respondió a tiempo.", ex);
        }
        catch (TimeoutRejectedException ex)
        {
            throw new GeoespacialNoDisponibleException("El servicio Geoespacial no respondió a tiempo.", ex);
        }
        catch (BrokenCircuitException ex)
        {
            // Circuito abierto (spec 002 RF-009): falla de inmediato, sin llamar a Geoespacial.
            throw new GeoespacialNoDisponibleException("El servicio Geoespacial no está disponible (circuito abierto).", ex);
        }

        using (respuesta)
        {
            if (respuesta.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK)
            {
                LocationAssigned? asignada = await respuesta.Content.ReadFromJsonAsync<LocationAssigned>(cancellationToken);
                if (asignada is null || asignada.CoordinateId == Guid.Empty)
                    throw new GeoespacialNoDisponibleException("El servicio Geoespacial devolvió una respuesta sin identificador de coordenada.");

                return asignada.CoordinateId;
            }

            if (respuesta.StatusCode == HttpStatusCode.BadRequest)
            {
                ErrorResponse? error = await LeerErrorAsync(respuesta, cancellationToken);
                string? motivo = error?.Details?.FirstOrDefault()?.Reason ?? error?.Message;
                throw new CoordenadaInvalidaException(
                    motivo is null ? "La coordenada no es válida." : $"La coordenada no es válida: {motivo}");
            }

            string cuerpo = await respuesta.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "Geoespacial respondió {StatusCode} al asignar la coordenada del reporte {IdReporte}: {Cuerpo}",
                (int)respuesta.StatusCode, idReporte, cuerpo);

            throw new GeoespacialNoDisponibleException(
                $"El servicio Geoespacial no pudo registrar la coordenada (HTTP {(int)respuesta.StatusCode}).");
        }
    }

    private static async Task<ErrorResponse?> LeerErrorAsync(HttpResponseMessage respuesta, CancellationToken cancellationToken)
    {
        try
        {
            return await respuesta.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private record AssignLocation(Guid ReportId, Coordinate Coordinate);

    private record Coordinate(double Lat, double Lon);

    private record LocationAssigned(Guid CoordinateId, Guid ReportId, Coordinate Coordinate);

    private record ErrorResponse(int Code, string Message, List<ErrorDetail>? Details);

    private record ErrorDetail(string Field, string Reason);
}
