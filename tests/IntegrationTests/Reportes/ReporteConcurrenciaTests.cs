using Application.Interfaces.Reportes;
using Domain.Reportes;
using Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Reportes;

public class ReporteConcurrenciaTests(ReportesApiFactory factory) : IClassFixture<ReportesApiFactory>
{
    [Fact]
    public async Task ActualizarAsync_LanzaConcurrencyException_SiElReporteFueModificadoPorOtraTransaccion()
    {
        Reporte reporte = new(
            CategoriaReporte.ViasYAndenes,
            TipoReporte.HuecosEnLaVia,
            "Hueco grande que afecta el tránsito vehicular",
            "https://imagenes.urbanalert.com/foto.jpg",
            null,
            Guid.NewGuid());
        reporte.AsignarCoordenada(Guid.NewGuid());

        using (IServiceScope scopeSemilla = factory.Services.CreateScope())
        {
            IReporteRepository repositorio = scopeSemilla.ServiceProvider.GetRequiredService<IReporteRepository>();
            await repositorio.AgregarAsync(reporte, CancellationToken.None);
        }

        using IServiceScope scopeDesactualizado = factory.Services.CreateScope();
        ReportesDbContext contextoDesactualizado = scopeDesactualizado.ServiceProvider.GetRequiredService<ReportesDbContext>();
        Reporte reporteDesactualizado = await contextoDesactualizado.Reportes.SingleAsync(r => r.Id == reporte.Id);

        using (IServiceScope scopeConcurrente = factory.Services.CreateScope())
        {
            ReportesDbContext contextoConcurrente = scopeConcurrente.ServiceProvider.GetRequiredService<ReportesDbContext>();
            Reporte reporteConcurrente = await contextoConcurrente.Reportes.SingleAsync(r => r.Id == reporte.Id);
            reporteConcurrente.ActualizarNivelEmergencia(NivelEmergencia.Alta);
            await contextoConcurrente.SaveChangesAsync();
        }

        reporteDesactualizado.ActualizarNivelEmergencia(NivelEmergencia.Baja);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextoDesactualizado.SaveChangesAsync());
    }
}
