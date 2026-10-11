namespace Application.Interfaces.Geoespacial;

public interface IGeoespacialClient
{
    /// <summary>
    /// Registra la coordenada del reporte en el servicio Geoespacial y devuelve el
    /// identificador de coordenada que este emite. Repetir la misma solicitud
    /// devuelve el mismo identificador.
    /// </summary>
    /// <exception cref="Application.Geoespacial.CoordenadaInvalidaException">La coordenada no es válida.</exception>
    /// <exception cref="Application.Geoespacial.GeoespacialNoDisponibleException">No fue posible registrar la coordenada.</exception>
    Task<Guid> AsignarCoordenadaAsync(Guid idReporte, double latitud, double longitud, CancellationToken cancellationToken);
}
