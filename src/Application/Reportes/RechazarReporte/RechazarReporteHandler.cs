using Application.Interfaces.Reportes;
using Application.Interfaces.RechazarReporte;
using Domain.Reportes;

namespace Application.Reportes.RechazarReporte;

public class RechazarReporteHandler(IReporteRepository reporteRepository) : IRechazarReporteHandler
{
    public async Task<bool> Handle(RechazarReporteCommand command, CancellationToken cancellationToken)
    {
        Reporte? reporte = await reporteRepository.ObtenerPorIdAsync(command.IdReporte, cancellationToken);
        
        if (reporte is null)
            return false;

        reporte.Rechazar(command.Motivo);
        await reporteRepository.ActualizarAsync(reporte, cancellationToken);

        return true;
    }
}
