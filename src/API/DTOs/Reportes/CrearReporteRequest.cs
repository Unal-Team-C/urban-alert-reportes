using Domain.Reportes;

namespace API.DTOs.Reportes;

public record CrearReporteRequest(
    CategoriaReporte Categoria,
    TipoReporte Tipo,
    string Descripcion,
    double Latitud,
    double Longitud,
    string UrlImagen,
    Guid? IdUsuario = null,
    // Prueba de verificación humana del portal (spec 003); obligatoria solo si Captcha:Habilitado.
    string? CaptchaToken = null);
