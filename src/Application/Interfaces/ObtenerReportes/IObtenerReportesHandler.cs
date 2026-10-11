using Application.Reportes;
using Application.Reportes.ObtenerReportes;

namespace Application.Interfaces.ObtenerReportes;

public interface IObtenerReportesHandler
{
    Task<PaginaDto<ReporteDto>> Handle(ObtenerReportesQuery query, CancellationToken cancellationToken);
}
