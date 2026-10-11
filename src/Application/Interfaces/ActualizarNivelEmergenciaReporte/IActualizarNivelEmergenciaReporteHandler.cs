using Application.Reportes.ActualizarNivelEmergenciaReporte;

namespace Application.Interfaces.ActualizarNivelEmergenciaReporte;

public interface IActualizarNivelEmergenciaReporteHandler
{
    Task<bool> Handle(ActualizarNivelEmergenciaReporteCommand command, CancellationToken cancellationToken);
}
