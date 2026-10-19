namespace Application.Captcha;

// El proveedor de verificación no respondió o falló: la creación se rechaza (falla cerrado, spec 003 RF-005).
public class CaptchaNoDisponibleException(string message, Exception? innerException = null)
    : Exception(message, innerException);
