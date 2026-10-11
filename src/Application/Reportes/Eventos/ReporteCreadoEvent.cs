using Domain.Reportes;

namespace Application.Reportes.Eventos;

public record ReporteCreadoEvent(Guid IdEvento, ReporteEventoDto Reporte);

public record ReporteEventoDto(Guid Id, EstadoReporte Estado, Guid IdCoordenada, Guid IdUsuario, DateTime Fecha);
