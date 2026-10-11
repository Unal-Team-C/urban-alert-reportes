using Application.Reportes;
using Application.Reportes.ObtenerReportePorId;

namespace Application.Interfaces.ObtenerReportePorId;

public interface IObtenerReportePorIdHandler
{
    Task<ReporteDto?> Handle(ObtenerReportePorIdQuery query, CancellationToken cancellationToken);
}
