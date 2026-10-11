using Application.Interfaces.ActualizarEstadoReporte;
using Application.Interfaces.ActualizarNivelEmergenciaReporte;
using Application.Interfaces.AsignarResponsableReporte;
using Application.Interfaces.CrearReporte;
using Application.Interfaces.EliminarReporte;
using Application.Interfaces.ObtenerReportePorId;
using Application.Interfaces.ObtenerReportes;
using Application.Interfaces.RechazarReporte;
using Application.Reportes.ActualizarEstadoReporte;
using Application.Reportes.ActualizarNivelEmergenciaReporte;
using Application.Reportes.AsignarResponsableReporte;
using Application.Reportes.CrearReporte;
using Application.Reportes.EliminarReporte;
using Application.Reportes.ObtenerReportePorId;
using Application.Reportes.ObtenerReportes;
using Application.Reportes.RechazarReporte;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICrearReporteHandler, CrearReporteHandler>();
        services.AddScoped<IObtenerReportesHandler, ObtenerReportesHandler>();
        services.AddScoped<IObtenerReportePorIdHandler, ObtenerReportePorIdHandler>();
        services.AddScoped<IActualizarEstadoReporteHandler, ActualizarEstadoReporteHandler>();
        services.AddScoped<IActualizarNivelEmergenciaReporteHandler, ActualizarNivelEmergenciaReporteHandler>();
        services.AddScoped<IAsignarResponsableReporteHandler, AsignarResponsableReporteHandler>();
        services.AddScoped<IRechazarReporteHandler, RechazarReporteHandler>();
        services.AddScoped<IEliminarReporteHandler, EliminarReporteHandler>();

        return services;
    }
}
