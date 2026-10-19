using Domain.Reportes;

namespace API.DTOs.Reportes;

// multipart/form-data: los datos del reporte y la imagen subida desde el dispositivo.
public record CrearReporteConImagenRequest(
    CategoriaReporte Categoria,
    TipoReporte Tipo,
    string Descripcion,
    double Latitud,
    double Longitud,
    IFormFile Imagen,
    Guid? IdUsuario = null,
    // Prueba de verificación humana del portal (spec 003); obligatoria solo si Captcha:Habilitado.
    string? CaptchaToken = null);
