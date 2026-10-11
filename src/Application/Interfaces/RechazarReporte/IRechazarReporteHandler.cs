using Application.Reportes.RechazarReporte;

namespace Application.Interfaces.RechazarReporte;

public interface IRechazarReporteHandler
{
    Task<bool> Handle(RechazarReporteCommand command, CancellationToken cancellationToken);
}
