using Application.Interfaces.Reportes;
using Domain.Reportes;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistencia.Repositorios;

public class ReporteRepository(ReportesDbContext dbContext) : IReporteRepository
{
    public async Task AgregarAsync(Reporte reporte, CancellationToken cancellationToken)
    {
        await dbContext.Reportes.AddAsync(reporte, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Reporte?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Reportes.FirstOrDefaultAsync(reporte => reporte.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<Reporte> Elementos, int Total)> ObtenerPaginadoAsync(
        EstadoReporte? estado,
        NivelEmergencia? nivelEmergencia,
        TipoReporte? tipo,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken)
    {
        IQueryable<Reporte> consulta = dbContext.Reportes.AsNoTracking();

        if (estado is not null)
            consulta = consulta.Where(reporte => reporte.Estado == estado);

        if (nivelEmergencia is not null)
            consulta = consulta.Where(reporte => reporte.NivelEmergencia == nivelEmergencia);

        if (tipo is not null)
            consulta = consulta.Where(reporte => reporte.Tipo == tipo);

        int total = await consulta.CountAsync(cancellationToken);

        List<Reporte> elementos = await consulta
            .OrderByDescending(reporte => reporte.Fecha)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(cancellationToken);

        return (elementos, total);
    }

    public Task ActualizarAsync(Reporte reporte, CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public Task EliminarAsync(Reporte reporte, CancellationToken cancellationToken)
    {
        dbContext.Reportes.Remove(reporte);
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
