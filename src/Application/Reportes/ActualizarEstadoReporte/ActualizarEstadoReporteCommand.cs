using Domain.Reportes;

namespace Application.Reportes.ActualizarEstadoReporte;

public record ActualizarEstadoReporteCommand(Guid IdReporte, EstadoReporte NuevoEstado);
