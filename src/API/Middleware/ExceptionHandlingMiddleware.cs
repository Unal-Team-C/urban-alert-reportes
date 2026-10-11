using System.Net;
using Application.Geoespacial;
using Application.Multimedia;
using Domain.Reportes;
using Microsoft.EntityFrameworkCore;

namespace API.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is TransicionEstadoInvalidaException or ArgumentException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = ex.Message });
        }
        catch (GeoespacialNoDisponibleException ex)
        {
            logger.LogWarning(ex, "No fue posible registrar la ubicación en Geoespacial.");
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { message = "No fue posible registrar la ubicación del reporte. Intenta nuevamente más tarde." });
        }
        catch (MultimediaNoDisponibleException ex)
        {
            logger.LogWarning(ex, "No fue posible subir la imagen al servicio Multimedia.");
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { message = "No fue posible subir la imagen del reporte. Intenta nuevamente más tarde." });
        }
        catch (BadHttpRequestException ex)
        {
            // Errores de la petición que detecta Kestrel, p. ej. 413 si el cuerpo supera el límite.
            context.Response.StatusCode = ex.StatusCode;
            await context.Response.WriteAsJsonAsync(new
            {
                message = ex.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? "La petición supera el tamaño máximo permitido."
                    : "La petición no es válida."
            });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Conflicto de concurrencia al actualizar un reporte.");
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            await context.Response.WriteAsJsonAsync(new { message = "El reporte fue modificado por otra solicitud. Vuelve a intentarlo." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error no controlado al procesar la solicitud {Method} {Path}.", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsJsonAsync(new { message = "Ocurrió un error inesperado. Intenta nuevamente más tarde." });
        }
    }
}
