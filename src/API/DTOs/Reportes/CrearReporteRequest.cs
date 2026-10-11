using Domain.Reportes;

namespace API.DTOs.Reportes;

public record CrearReporteRequest(
    CategoriaReporte Categoria,
    TipoReporte Tipo,
    string Descripcion,
    double Latitud,
    double Longitud,
    string UrlImagen,
    Guid? IdUsuario = null);
