using Application.Interfaces.ObtenerReportePorId;
using Application.Interfaces.Reportes;
using Domain.Reportes;

namespace Application.Reportes.ObtenerReportePorId;

public class ObtenerReportePorIdHandler(IReporteRepository reporteRepository) : IObtenerReportePorIdHandler
{
    public async Task<ReporteDto?> Handle(ObtenerReportePorIdQuery query, CancellationToken cancellationToken)
    {
        Reporte? reporte = await reporteRepository.ObtenerPorIdAsync(query.Id, cancellationToken);

        return reporte is null ? null : ReporteDto.DesdeEntidad(reporte);
    }
}
