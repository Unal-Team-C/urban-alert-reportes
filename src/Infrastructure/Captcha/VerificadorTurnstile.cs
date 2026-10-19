using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Captcha;
using Application.Interfaces.Captcha;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Infrastructure.Captcha;

// Cloudflare Turnstile: POST siteverify (form-urlencoded secret + response) → {success, hostname, action, error-codes}.
// La prueba es de un solo uso: el pipeline solo reintenta si la conexión no se estableció (plan D-003). Cualquier fallo
// del proveedor rechaza la creación (falla cerrado, RF-005). Ni la prueba ni la clave secreta se escriben en el log.
public class VerificadorTurnstile(
    HttpClient httpClient,
    CaptchaOpciones opciones,
    ILogger<VerificadorTurnstile> logger) : IVerificadorCaptcha
{
    public const int LongitudMaxima = 2048;

    // Errores de nuestra configuración o del proveedor, no de quien envía la petición.
    private static readonly HashSet<string> ErroresDelServicio =
        new(["missing-input-secret", "invalid-input-secret", "internal-error"], StringComparer.Ordinal);

    public async Task VerificarAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            Rechazar("missing-input-response");
        if (token!.Length > LongitudMaxima)
            Rechazar("token-too-long");

        RespuestaSiteverify respuesta = await ConsultarAsync(token.Trim(), cancellationToken);

        if (!respuesta.Success)
        {
            string[] codigos = respuesta.ErrorCodes ?? [];
            if (codigos.Any(ErroresDelServicio.Contains))
            {
                logger.LogError("Turnstile no pudo verificar por un error del servicio o de configuración: {Codigos}", string.Join(",", codigos));
                throw new CaptchaNoDisponibleException("El proveedor de verificación respondió con un error del servicio.");
            }

            Rechazar(codigos.Length > 0 ? string.Join(",", codigos) : "success=false");
        }

        if (opciones.HostnamesPermitidos.Count > 0 &&
            !opciones.HostnamesPermitidos.Contains(respuesta.Hostname ?? "", StringComparer.OrdinalIgnoreCase))
            Rechazar($"hostname-no-permitido ({respuesta.Hostname})");

        if (opciones.Accion.Length > 0 && !string.Equals(respuesta.Action, opciones.Accion, StringComparison.Ordinal))
            Rechazar($"accion-no-esperada ({respuesta.Action})");
    }

    private async Task<RespuestaSiteverify> ConsultarAsync(string token, CancellationToken cancellationToken)
    {
        using FormUrlEncodedContent cuerpo = new([
            new KeyValuePair<string, string>("secret", opciones.ClaveSecreta),
            new KeyValuePair<string, string>("response", token)
        ]);

        HttpResponseMessage http;
        try
        {
            http = await httpClient.PostAsync(opciones.UrlVerificacion, cuerpo, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new CaptchaNoDisponibleException("No fue posible conectar con el proveedor de verificación.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CaptchaNoDisponibleException("El proveedor de verificación no respondió a tiempo.", ex);
        }
        catch (TimeoutRejectedException ex)
        {
            throw new CaptchaNoDisponibleException("El proveedor de verificación no respondió a tiempo.", ex);
        }
        catch (BrokenCircuitException ex)
        {
            throw new CaptchaNoDisponibleException("El proveedor de verificación no está disponible (circuito abierto).", ex);
        }

        using (http)
        {
            if (!http.IsSuccessStatusCode)
            {
                logger.LogWarning("Turnstile respondió {StatusCode} a la verificación", (int)http.StatusCode);
                throw new CaptchaNoDisponibleException($"El proveedor de verificación respondió HTTP {(int)http.StatusCode}.");
            }

            try
            {
                return await http.Content.ReadFromJsonAsync<RespuestaSiteverify>(cancellationToken)
                       ?? throw new CaptchaNoDisponibleException("El proveedor de verificación devolvió una respuesta vacía.");
            }
            catch (JsonException ex)
            {
                throw new CaptchaNoDisponibleException("El proveedor de verificación devolvió una respuesta ilegible.", ex);
            }
        }
    }

    private void Rechazar(string motivo)
    {
        logger.LogWarning("Verificación humana rechazada: {Motivo}", motivo);
        throw new CaptchaInvalidoException(motivo);
    }

    private sealed record RespuestaSiteverify(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("hostname")] string? Hostname,
        [property: JsonPropertyName("action")] string? Action,
        [property: JsonPropertyName("error-codes")] string[]? ErrorCodes);
}
