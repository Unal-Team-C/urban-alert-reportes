using Application.Reportes.CrearReporte;

namespace Application.Interfaces.CrearReporte;

public interface ICrearReporteHandler
{
    Task<Guid> Handle(CrearReporteCommand command, CancellationToken cancellationToken);
}
