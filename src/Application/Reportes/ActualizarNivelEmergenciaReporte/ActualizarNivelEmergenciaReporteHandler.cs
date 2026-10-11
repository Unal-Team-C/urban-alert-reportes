using Application.Interfaces.ActualizarNivelEmergenciaReporte;
using Application.Interfaces.Reportes;
using Domain.Reportes;

namespace Application.Reportes.ActualizarNivelEmergenciaReporte;

public class ActualizarNivelEmergenciaReporteHandler(IReporteRepository reporteRepository)
    : IActualizarNivelEmergenciaReporteHandler
{
    public async Task<bool> Handle(ActualizarNivelEmergenciaReporteCommand command, CancellationToken cancellationToken)
    {
        Reporte? reporte = await reporteRepository.ObtenerPorIdAsync(command.IdReporte, cancellationToken);
        if (reporte is null)
        {
            return false;
        }

        reporte.ActualizarNivelEmergencia(command.NivelEmergencia);
        await reporteRepository.ActualizarAsync(reporte, cancellationToken);

        return true;
    }
}
