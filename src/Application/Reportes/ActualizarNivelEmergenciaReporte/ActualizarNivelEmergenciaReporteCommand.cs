using Domain.Reportes;

namespace Application.Reportes.ActualizarNivelEmergenciaReporte;

public record ActualizarNivelEmergenciaReporteCommand(Guid IdReporte, NivelEmergencia NivelEmergencia);
