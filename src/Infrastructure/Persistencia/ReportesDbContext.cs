using Domain.Reportes;
using Infrastructure.Mensajeria.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistencia;

public class ReportesDbContext : DbContext
{
    public ReportesDbContext(DbContextOptions<ReportesDbContext> options) : base(options) { }

    public DbSet<Reporte> Reportes => Set<Reporte>();

    public DbSet<MensajeOutbox> MensajesOutbox => Set<MensajeOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportesDbContext).Assembly);
    }
}
