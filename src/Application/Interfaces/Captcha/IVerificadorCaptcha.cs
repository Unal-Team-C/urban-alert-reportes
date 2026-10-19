namespace Application.Interfaces.Captcha;

// Comprueba que la creación viene de una persona que superó el desafío del portal (spec 003).
// Lanza CaptchaInvalidoException (400) o CaptchaNoDisponibleException (503); si la verificación está
// desactivada no hace nada.
public interface IVerificadorCaptcha
{
    Task VerificarAsync(string? token, CancellationToken cancellationToken);
}
