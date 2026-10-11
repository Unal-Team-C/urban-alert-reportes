using Application.Reportes.AsignarResponsableReporte;

namespace Application.Interfaces.AsignarResponsableReporte;

public interface IAsignarResponsableReporteHandler
{
    Task<bool> Handle(AsignarResponsableReporteCommand command, CancellationToken cancellationToken);
}
