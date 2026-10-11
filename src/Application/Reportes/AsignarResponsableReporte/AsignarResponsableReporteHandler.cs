using Application.Interfaces.AsignarResponsableReporte;
using Application.Interfaces.Reportes;
using Domain.Reportes;

namespace Application.Reportes.AsignarResponsableReporte;

public class AsignarResponsableReporteHandler(IReporteRepository reporteRepository) : IAsignarResponsableReporteHandler
{
    public async Task<bool> Handle(AsignarResponsableReporteCommand command, CancellationToken cancellationToken)
    {
        Reporte? reporte = await reporteRepository.ObtenerPorIdAsync(command.IdReporte, cancellationToken);
        if (reporte is null)
        {
            return false;
        }

        reporte.AsignarResponsable(command.IdResponsable);
        await reporteRepository.ActualizarAsync(reporte, cancellationToken);

        return true;
    }
}
