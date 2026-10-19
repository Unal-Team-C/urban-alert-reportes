using Microsoft.Extensions.Configuration;

namespace Infrastructure.Captcha;

// Sección "Captcha" (spec 003 RF-007, RF-008). Desactivada por defecto: se activa cuando el portal ya envía la prueba
// y existe la clave secreta (en AWS, del secreto de arranque urban-alert-reportes-captcha; plan D-002).
public sealed record CaptchaOpciones(
    bool Habilitado,
    string ClaveSecreta,
    IReadOnlyList<string> HostnamesPermitidos,
    string Accion,
    string UrlVerificacion,
    int TimeoutSegundos)
{
    public const string UrlTurnstile = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public static CaptchaOpciones Desde(IConfiguration configuration)
    {
        IConfigurationSection seccion = configuration.GetSection("Captcha");
        CaptchaOpciones opciones = new(
            seccion.GetValue("Habilitado", false),
            seccion["ClaveSecreta"] ?? "",
            seccion.GetSection("HostnamesPermitidos").Get<string[]>()?.Where(h => !string.IsNullOrWhiteSpace(h)).ToArray() ?? [],
            seccion["Accion"] ?? "",
            seccion["UrlVerificacion"] is { Length: > 0 } url ? url : UrlTurnstile,
            seccion.GetValue("TimeoutSegundos", 5));

        // Activada sin clave rechazaría todas las creaciones: mejor que la API no arranque y Provision falle.
        if (opciones.Habilitado && string.IsNullOrWhiteSpace(opciones.ClaveSecreta))
            throw new InvalidOperationException("Captcha:Habilitado=true exige 'Captcha:ClaveSecreta' (en AWS, el secreto urban-alert-reportes-captcha).");
        if (opciones.TimeoutSegundos < 1)
            throw new InvalidOperationException("Captcha:TimeoutSegundos debe ser mayor que cero.");

        return opciones;
    }
}
