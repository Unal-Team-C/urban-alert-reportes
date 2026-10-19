namespace Application.Captcha;

// La prueba de verificación humana falta, no es válida, venció, ya se usó o es de otro sitio (spec 003 RF-003, RF-004).
public class CaptchaInvalidoException(string motivo)
    : Exception($"Verificación humana rechazada: {motivo}")
{
    public string Motivo { get; } = motivo;
}
