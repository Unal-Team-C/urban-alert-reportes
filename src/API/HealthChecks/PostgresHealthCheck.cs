using Infrastructure.Persistencia;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace API.HealthChecks;

public class PostgresHealthCheck(ReportesDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            bool puedeConectar = await dbContext.Database.CanConnectAsync(cancellationToken);

            return puedeConectar
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("No se pudo conectar a la base de datos.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Error al verificar la conexión a la base de datos.", ex);
        }
    }
}
