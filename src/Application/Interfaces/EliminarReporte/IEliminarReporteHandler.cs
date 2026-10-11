using Application.Reportes.EliminarReporte;

namespace Application.Interfaces.EliminarReporte;

public interface IEliminarReporteHandler
{
    Task<bool> Handle(EliminarReporteCommand command, CancellationToken cancellationToken);
}
