using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Imagenes;
using Application.Interfaces.Multimedia;
using Application.Multimedia;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Multimedia;

// Multimedia corre como función Lambda (Python + FastAPI, Mangum). En AWS, API Gateway traduce la
// petición HTTP al evento que recibe Lambda; en local, sin API Gateway, el Runtime Interface
// Emulator del propio contenedor expone /2015-03-31/functions/function/invocations y espera
// directamente ese evento (API Gateway HTTP API, payload 2.0) con el cuerpo en base64 — igual que
// scripts/invoke-upload.sh del servicio Multimedia construye a mano.
public class MultimediaHttpClient(HttpClient httpClient, ILogger<MultimediaHttpClient> logger) : IMultimediaClient
{
    private const string RutaInvocacion = "2015-03-31/functions/function/invocations";
    private const string RutaSubida = "/api/v1/multimedia/images";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<string> SubirImagenAsync(
        Stream contenido,
        long tamano,
        string nombreArchivo,
        string tipoContenido,
        double? latitud,
        double? longitud,
        CancellationToken cancellationToken)
    {
        (byte[] cuerpo, string boundary) = await ConstruirMultipartAsync(
            contenido, nombreArchivo, tipoContenido, latitud, longitud, cancellationToken);

        object evento = new
        {
            Version = "2.0",
            RouteKey = "$default",
            RawPath = RutaSubida,
            RawQueryString = "",
            Headers = new Dictionary<string, string>
            {
                ["content-type"] = $"multipart/form-data; boundary={boundary}",
                ["content-length"] = cuerpo.Length.ToString(CultureInfo.InvariantCulture)
            },
            RequestContext = new
            {
                AccountId = "000000000000",
                ApiId = "local",
                DomainName = "localhost",
                DomainPrefix = "localhost",
                Http = new
                {
                    Method = "POST",
                    Path = RutaSubida,
                    Protocol = "HTTP/1.1",
                    SourceIp = "127.0.0.1",
                    UserAgent = "UrbanAlert.Reportes"
                },
                RequestId = Guid.NewGuid().ToString(),
                RouteKey = "$default",
                Stage = "$default",
                Time = DateTimeOffset.UtcNow.ToString("dd/MMM/yyyy:HH:mm:ss zzz", CultureInfo.InvariantCulture),
                TimeEpoch = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            },
            Body = Convert.ToBase64String(cuerpo),
            IsBase64Encoded = true
        };

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await httpClient.PostAsJsonAsync(RutaInvocacion, evento, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new MultimediaNoDisponibleException("No fue posible conectar con el servicio Multimedia.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MultimediaNoDisponibleException("El servicio Multimedia no respondió a tiempo.", ex);
        }

        using (respuesta)
        {
            if (!respuesta.IsSuccessStatusCode)
            {
                string cuerpoError = await respuesta.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "El invocador de Multimedia respondió {StatusCode} al subir una imagen: {Cuerpo}",
                    (int)respuesta.StatusCode, cuerpoError);
                throw new MultimediaNoDisponibleException(
                    $"El servicio Multimedia no pudo recibir la invocación (HTTP {(int)respuesta.StatusCode}).");
            }

            InvocationResponse? invocacion = await respuesta.Content.ReadFromJsonAsync<InvocationResponse>(OpcionesJson, cancellationToken);
            if (invocacion is null)
                throw new MultimediaNoDisponibleException("El servicio Multimedia devolvió una respuesta vacía.");

            string? cuerpoJson = invocacion.IsBase64Encoded && invocacion.Body is not null
                ? Encoding.UTF8.GetString(Convert.FromBase64String(invocacion.Body))
                : invocacion.Body;

            if (invocacion.StatusCode == 201)
            {
                UploadImageResponse? subida = DeserializarSeguro<UploadImageResponse>(cuerpoJson);
                if (subida is null || string.IsNullOrWhiteSpace(subida.ImageUrl))
                    throw new MultimediaNoDisponibleException("El servicio Multimedia devolvió una respuesta sin la URL de la imagen.");

                return subida.ImageUrl;
            }

            if (invocacion.StatusCode is 400 or 413 or 415)
            {
                ErrorResponse? error = DeserializarSeguro<ErrorResponse>(cuerpoJson);
                string? motivo = error?.Details?.FirstOrDefault()?.Reason ?? error?.Message;
                throw new ImagenInvalidaException(
                    motivo is null ? "La imagen no es válida." : $"La imagen no es válida: {motivo}");
            }

            logger.LogWarning("Multimedia respondió {StatusCode} al subir una imagen: {Cuerpo}", invocacion.StatusCode, cuerpoJson);
            throw new MultimediaNoDisponibleException(
                $"El servicio Multimedia no pudo guardar la imagen (HTTP {invocacion.StatusCode}).");
        }
    }

    // multipart/form-data a mano: lat/lon (datos de captura, opcionales) y el archivo de imagen.
    private static async Task<(byte[] Cuerpo, string Boundary)> ConstruirMultipartAsync(
        Stream contenido,
        string nombreArchivo,
        string tipoContenido,
        double? latitud,
        double? longitud,
        CancellationToken cancellationToken)
    {
        string boundary = $"----urbanalert{Guid.NewGuid():N}";
        using MemoryStream cuerpo = new();

        async Task EscribirAsync(string texto)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(texto);
            await cuerpo.WriteAsync(bytes, cancellationToken);
        }

        async Task EscribirCampoAsync(string nombre, string valor) =>
            await EscribirAsync($"--{boundary}\r\nContent-Disposition: form-data; name=\"{nombre}\"\r\n\r\n{valor}\r\n");

        if (latitud is not null && longitud is not null)
        {
            await EscribirCampoAsync("lat", latitud.Value.ToString(CultureInfo.InvariantCulture));
            await EscribirCampoAsync("lon", longitud.Value.ToString(CultureInfo.InvariantCulture));
        }

        await EscribirAsync(
            $"--{boundary}\r\nContent-Disposition: form-data; name=\"image\"; filename=\"{nombreArchivo}\"\r\nContent-Type: {tipoContenido}\r\n\r\n");

        contenido.Position = 0;
        await contenido.CopyToAsync(cuerpo, cancellationToken);

        await EscribirAsync($"\r\n--{boundary}--\r\n");

        return (cuerpo.ToArray(), boundary);
    }

    private static T? DeserializarSeguro<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json, OpcionesJson);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    // Sobre de respuesta del Runtime Interface Emulator (statusCode/body/headers de la función).
    private sealed record InvocationResponse(int StatusCode, string? Body, bool IsBase64Encoded);

    private sealed record UploadImageResponse(string ImageId, string ImageUrl, string ContentType, long SizeBytes);

    private sealed record ErrorResponse(int Code, string Message, List<ErrorDetail>? Details);

    private sealed record ErrorDetail(string Field, string Reason);
}
