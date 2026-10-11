namespace Application.Imagenes;

// Valida la imagen de un reporte por su contenido real (primeros bytes), no por el
// nombre ni el Content-Type que declara el cliente. Mismas reglas que acepta el servicio
// Multimedia (JPEG/PNG, 3,5 MB), que es quien aloja la imagen: validar más permisivo aquí
// solo retrasaría el mismo rechazo hasta después de subirla.
public static class ValidadorImagen
{
    public const long TamanoMaximoBytes = 3_670_016;

    private static readonly byte[] FirmaJpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Devuelve el formato de la imagen y deja el stream en su posición inicial.
    /// </summary>
    /// <exception cref="ImagenInvalidaException">Vacía, demasiado grande o en un formato no admitido.</exception>
    public static async Task<FormatoImagen> ValidarAsync(Stream contenido, long tamano, CancellationToken cancellationToken)
    {
        if (tamano <= 0)
            throw new ImagenInvalidaException("La imagen del reporte es obligatoria.");

        if (tamano > TamanoMaximoBytes)
            throw new ImagenInvalidaException("La imagen no puede superar 3,5 MB.");

        if (!contenido.CanSeek)
            throw new InvalidOperationException("El contenido de la imagen debe permitir volver al inicio.");

        byte[] cabecera = new byte[8];
        long posicionInicial = contenido.Position;
        int leidos = await contenido.ReadAtLeastAsync(cabecera, cabecera.Length, throwOnEndOfStream: false, cancellationToken);
        contenido.Position = posicionInicial;

        ReadOnlySpan<byte> bytes = cabecera.AsSpan(0, leidos);

        if (bytes.StartsWith(FirmaJpeg))
            return FormatoImagen.Jpeg;

        if (bytes.StartsWith(FirmaPng))
            return FormatoImagen.Png;

        throw new ImagenInvalidaException("La imagen debe estar en formato JPEG o PNG.");
    }
}
