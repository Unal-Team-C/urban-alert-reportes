using Domain.Reportes;

namespace Application.Reportes.CrearReporte;

// La imagen llega de una de dos formas: como URL ya alojada (JSON) o como archivo subido
// desde el dispositivo (multipart). Se exige al menos una.
// IdUsuario: quién crea el reporte. Mientras no exista autenticación lo envía el cliente;
// si no llega, se asigna un usuario genérico.
public record CrearReporteCommand(
    CategoriaReporte Categoria,
    TipoReporte Tipo,
    string Descripcion,
    double Latitud,
    double Longitud,
    string? UrlImagen,
    ImagenAdjunta? Imagen = null,
    Guid? IdUsuario = null
);

// El stream debe permitir volver al inicio (Seek): primero se lee su cabecera para validarlo.
public record ImagenAdjunta(Stream Contenido, long Tamano, string NombreArchivo);
