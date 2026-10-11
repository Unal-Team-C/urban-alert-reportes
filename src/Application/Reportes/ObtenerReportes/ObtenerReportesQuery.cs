using Domain.Reportes;

namespace Application.Reportes.ObtenerReportes;

public record ObtenerReportesQuery(
    EstadoReporte? Estado,
    NivelEmergencia? NivelEmergencia,
    TipoReporte? Tipo,
    int Pagina,
    int TamanoPagina);
