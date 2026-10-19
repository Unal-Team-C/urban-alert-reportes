using Application.Interfaces.Eventos;
using Domain.Reportes;

namespace Application.Interfaces.Reportes;

public interface IReporteRepository
{
    // Guarda el reporte y su evento de creación en la misma transacción: o quedan ambos o ninguno
    // (spec 002 RF-001). El evento lo entrega después el despachador del outbox.
    Task AgregarAsync(Reporte reporte, IEventoDominio eventoCreacion, CancellationToken cancellationToken);

    Task<Reporte?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Reporte> Elementos, int Total)> ObtenerPaginadoAsync(
        EstadoReporte? estado,
        NivelEmergencia? nivelEmergencia,
        TipoReporte? tipo,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken);

    Task ActualizarAsync(Reporte reporte, CancellationToken cancellationToken);

    Task EliminarAsync(Reporte reporte, CancellationToken cancellationToken);
}
