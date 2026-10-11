namespace Application.Interfaces.Multimedia;

public interface IMultimediaClient
{
    /// <summary>
    /// Sube la imagen al servicio Multimedia y devuelve la URL definitiva donde queda alojada.
    /// </summary>
    /// <exception cref="Application.Imagenes.ImagenInvalidaException">Multimedia rechazó la imagen (formato, tamaño u otro parámetro inválido).</exception>
    /// <exception cref="Application.Multimedia.MultimediaNoDisponibleException">No fue posible subir la imagen.</exception>
    Task<string> SubirImagenAsync(
        Stream contenido,
        long tamano,
        string nombreArchivo,
        string tipoContenido,
        double? latitud,
        double? longitud,
        CancellationToken cancellationToken);
}
