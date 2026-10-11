using Application.Reportes.ActualizarEstadoReporte;

namespace Application.Interfaces.ActualizarEstadoReporte;

public interface IActualizarEstadoReporteHandler
{
    Task<bool> Handle(ActualizarEstadoReporteCommand command, CancellationToken cancellationToken);
}
