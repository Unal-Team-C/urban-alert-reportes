using Application.Interfaces.EliminarReporte;
using Application.Interfaces.Reportes;
using Domain.Reportes;

namespace Application.Reportes.EliminarReporte;

public class EliminarReporteHandler(IReporteRepository reporteRepository) : IEliminarReporteHandler
{
    public async Task<bool> Handle(EliminarReporteCommand command, CancellationToken cancellationToken)
    {
        Reporte? reporte = await reporteRepository.ObtenerPorIdAsync(command.IdReporte, cancellationToken);
        if (reporte is null)
        {
            return false;
        }

        await reporteRepository.EliminarAsync(reporte, cancellationToken);

        return true;
    }
}
