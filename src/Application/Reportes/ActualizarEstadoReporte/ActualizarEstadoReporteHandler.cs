using Application.Interfaces.ActualizarEstadoReporte;
using Application.Interfaces.Reportes;
using Domain.Reportes;

namespace Application.Reportes.ActualizarEstadoReporte;

public class ActualizarEstadoReporteHandler(IReporteRepository reporteRepository) : IActualizarEstadoReporteHandler
{
    public async Task<bool> Handle(ActualizarEstadoReporteCommand command, CancellationToken cancellationToken)
    {
        Reporte? reporte = await reporteRepository
            .ObtenerPorIdAsync(command.IdReporte, cancellationToken);
        
        if (reporte is null)
            return false;

        reporte.ActualizarEstado(command.NuevoEstado);
        await reporteRepository.ActualizarAsync(reporte, cancellationToken);

        return true;
    }
}
