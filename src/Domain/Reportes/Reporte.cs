namespace Domain.Reportes;

public class Reporte
{
    public const int DescripcionMaxLength = 2000;
    public const int UrlImagenMaxLength = 2000;
    public const int NombreImagenMaxLength = 255;
    public const int MotivoRechazoMaxLength = 2000;

    private static readonly Dictionary<EstadoReporte, EstadoReporte> SiguienteEstado = new()
    {
        [EstadoReporte.Reportado] = EstadoReporte.Verificado,
        [EstadoReporte.Verificado] = EstadoReporte.Asignado,
        [EstadoReporte.Asignado] = EstadoReporte.EnIntervencion,
        [EstadoReporte.EnIntervencion] = EstadoReporte.Resuelto
    };

    public Guid Id { get; private set; }
    public CategoriaReporte Categoria { get; private set; }
    public TipoReporte Tipo { get; private set; }
    public string Descripcion { get; private set; } = null!;
    public Guid IdCoordenada { get; private set; }
    // Ruta donde está alojada la imagen. La envían directamente los clientes que usan JSON; para
    // las imágenes subidas desde el dispositivo la devuelve el servicio Multimedia al subirlas.
    public string? UrlImagen { get; private set; }
    // Nombre original del archivo subido desde el dispositivo junto con el reporte.
    public string? NombreImagen { get; private set; }
    public Guid IdUsuario { get; private set; }
    public NivelEmergencia NivelEmergencia { get; private set; }
    public EstadoReporte Estado { get; private set; }
    public DateTime Fecha { get; private set; }
    public Guid? IdResponsable { get; private set; }
    public string? MotivoRechazo { get; private set; }

    private Reporte() { }

    public Reporte(CategoriaReporte categoria,
        TipoReporte tipo,
        string descripcion,
        string? urlImagen,
        string? nombreImagen,
        Guid idUsuario)
    {
        if (!Enum.IsDefined(categoria))
            throw new ArgumentException("La categoría de reporte no es válida.", nameof(categoria));

        if (!Enum.IsDefined(tipo))
            throw new ArgumentException("El tipo de reporte no es válido.", nameof(tipo));

        if (!CatalogoReportes.PerteneceA(tipo, categoria))
            throw new ArgumentException(
                $"El tipo de reporte '{CatalogoReportes.Nombre(tipo)}' no pertenece a la categoría '{CatalogoReportes.Nombre(categoria)}'.",
                nameof(tipo));

        if (string.IsNullOrWhiteSpace(descripcion))
            throw new ArgumentException("La descripción es obligatoria.", nameof(descripcion));

        if (descripcion.Length > DescripcionMaxLength)
            throw new ArgumentException($"La descripción no puede superar {DescripcionMaxLength} caracteres.", nameof(descripcion));

        if (string.IsNullOrWhiteSpace(urlImagen) && string.IsNullOrWhiteSpace(nombreImagen))
            throw new ArgumentException("La imagen del reporte es obligatoria: su URL o el archivo.", nameof(urlImagen));

        if (!string.IsNullOrWhiteSpace(urlImagen))
            ValidarUrlImagen(urlImagen);

        if (!string.IsNullOrWhiteSpace(nombreImagen))
            ValidarNombreImagen(nombreImagen);

        if (idUsuario == Guid.Empty)
            throw new ArgumentException("El identificador del usuario es obligatorio.", nameof(idUsuario));

        Id = Guid.CreateVersion7();
        Categoria = categoria;
        Tipo = tipo;
        Descripcion = descripcion;
        UrlImagen = string.IsNullOrWhiteSpace(urlImagen) ? null : urlImagen;
        NombreImagen = string.IsNullOrWhiteSpace(nombreImagen) ? null : nombreImagen;
        IdUsuario = idUsuario;
        NivelEmergencia = NivelEmergencia.Default;
        Estado = EstadoReporte.Reportado;
        Fecha = DateTime.UtcNow;
    }

    // La coordenada la emite el servicio Geoespacial a partir del Id del reporte,
    // por eso se asigna después de construir (y validar) el reporte.
    public void AsignarCoordenada(Guid idCoordenada)
    {
        if (idCoordenada == Guid.Empty)
            throw new ArgumentException("El identificador de la coordenada es obligatorio.", nameof(idCoordenada));

        if (IdCoordenada != Guid.Empty)
            throw new InvalidOperationException("El reporte ya tiene una coordenada asignada.");

        IdCoordenada = idCoordenada;
    }

    // La URL definitiva la emite el servicio Multimedia a partir del archivo subido, por eso se
    // asigna después de construir (y validar) el reporte, igual que la coordenada de Geoespacial.
    public void AsignarUrlImagen(string urlImagen)
    {
        if (string.IsNullOrWhiteSpace(urlImagen))
            throw new ArgumentException("La URL de la imagen es obligatoria.", nameof(urlImagen));

        if (UrlImagen is not null)
            throw new InvalidOperationException("El reporte ya tiene una URL de imagen asignada.");

        ValidarUrlImagen(urlImagen);
        UrlImagen = urlImagen;
    }

    public void ActualizarEstado(EstadoReporte nuevoEstado)
    {
        if (!SiguienteEstado.TryGetValue(Estado, out EstadoReporte estadoEsperado) || estadoEsperado != nuevoEstado)
            throw new TransicionEstadoInvalidaException(Estado, nuevoEstado);

        Estado = nuevoEstado;
    }

    public void AsignarResponsable(Guid idResponsable)
    {
        if (idResponsable == Guid.Empty)
            throw new ArgumentException("El identificador del responsable es obligatorio.", nameof(idResponsable));

        IdResponsable = idResponsable;
    }

    public void ActualizarNivelEmergencia(NivelEmergencia nivelEmergencia)
    {
        NivelEmergencia = nivelEmergencia;
    }

    public void Rechazar(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("El motivo de rechazo es obligatorio.", nameof(motivo));

        if (motivo.Length > MotivoRechazoMaxLength)
            throw new ArgumentException($"El motivo de rechazo no puede superar {MotivoRechazoMaxLength} caracteres.", nameof(motivo));

        if (Estado is EstadoReporte.Resuelto or EstadoReporte.Rechazado)
            throw new TransicionEstadoInvalidaException(Estado, EstadoReporte.Rechazado);

        Estado = EstadoReporte.Rechazado;
        MotivoRechazo = motivo;
    }

    private static void ValidarUrlImagen(string urlImagen)
    {
        if (urlImagen.Length > UrlImagenMaxLength)
            throw new ArgumentException($"La URL de la imagen no puede superar {UrlImagenMaxLength} caracteres.", nameof(urlImagen));

        if (!Uri.TryCreate(urlImagen, UriKind.Absolute, out _))
            throw new ArgumentException("La URL de la imagen no es una URL absoluta válida.", nameof(urlImagen));
    }

    private static void ValidarNombreImagen(string nombreImagen)
    {
        if (nombreImagen.Length > NombreImagenMaxLength)
            throw new ArgumentException($"El nombre de la imagen no puede superar {NombreImagenMaxLength} caracteres.", nameof(nombreImagen));

        // Es solo un nombre de archivo, nunca una ruta.
        if (nombreImagen.IndexOfAny(['/', '\\']) >= 0 || nombreImagen is "." or "..")
            throw new ArgumentException("El nombre de la imagen no es válido.", nameof(nombreImagen));
    }
}
