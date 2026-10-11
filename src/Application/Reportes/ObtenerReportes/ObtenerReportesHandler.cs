using Application.Interfaces.ObtenerReportes;
using Application.Interfaces.Reportes;
using Domain.Reportes;

namespace Application.Reportes.ObtenerReportes;

public class ObtenerReportesHandler(IReporteRepository reporteRepository) : IObtenerReportesHandler
{
    public async Task<PaginaDto<ReporteDto>> Handle(ObtenerReportesQuery query, CancellationToken cancellationToken)
    {
        (IReadOnlyList<Reporte> elementos, int total) = await reporteRepository.ObtenerPaginadoAsync(
            query.Estado, query.NivelEmergencia, query.Tipo, query.Pagina, query.TamanoPagina, cancellationToken);

        return new PaginaDto<ReporteDto>(
            elementos.Select(ReporteDto.DesdeEntidad).ToList(),
            query.Pagina,
            query.TamanoPagina,
            total);
    }
}
