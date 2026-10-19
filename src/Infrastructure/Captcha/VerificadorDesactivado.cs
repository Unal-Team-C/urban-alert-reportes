using Application.Interfaces.Captcha;

namespace Infrastructure.Captcha;

// Captcha:Habilitado=false (por defecto): la creación se comporta como antes de la spec 003 (RF-007).
public sealed class VerificadorDesactivado : IVerificadorCaptcha
{
    public Task VerificarAsync(string? token, CancellationToken cancellationToken) => Task.CompletedTask;
}
