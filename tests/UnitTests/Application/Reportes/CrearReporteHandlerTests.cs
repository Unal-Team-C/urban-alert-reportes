using Application.Geoespacial;
using Application.Imagenes;
using Application.Interfaces.Eventos;
using Application.Interfaces.Geoespacial;
using Application.Interfaces.Multimedia;
using Application.Interfaces.Reportes;
using Application.Multimedia;
using Application.Reportes.CrearReporte;
using Application.Reportes.Eventos;
using Domain.Reportes;
using UnitTests.Application.Imagenes;

namespace UnitTests.Application.Reportes;

public class CrearReporteHandlerTests
{
    private readonly RepositorioFalso _repositorio = new();
    private readonly GeoespacialFalso _geoespacial = new();
    private readonly MultimediaFalso _multimedia = new();
    private readonly PublicadorFalso _publicador = new();

    private CrearReporteHandler CrearHandler() => new(_repositorio, _geoespacial, _multimedia, _publicador);

    private static CrearReporteCommand ComandoValido() => new(
        CategoriaReporte.ViasYAndenes,
        TipoReporte.HuecosEnLaVia,
        "Hueco grande que afecta el tránsito vehicular",
        4.6512,
        -74.0561,
        "https://imagenes.urbanalert.com/foto.jpg");

    [Fact]
    public async Task Handle_RegistraLaCoordenadaEnGeoespacialYLaGuardaEnElReporte()
    {
        Guid idReporte = await CrearHandler().Handle(ComandoValido(), CancellationToken.None);

        Reporte guardado = Assert.Single(_repositorio.Agregados);
        Assert.Equal(idReporte, guardado.Id);
        Assert.Equal(_geoespacial.IdCoordenadaDevuelto, guardado.IdCoordenada);

        (Guid idReporteEnviado, double latitud, double longitud) = Assert.Single(_geoespacial.Llamadas);
        Assert.Equal(idReporte, idReporteEnviado);
        Assert.Equal(4.6512, latitud);
        Assert.Equal(-74.0561, longitud);
    }

    [Fact]
    public async Task Handle_PublicaReporteCreadoConLaCoordenadaDeGeoespacial()
    {
        Guid idReporte = await CrearHandler().Handle(ComandoValido(), CancellationToken.None);

        ReporteCreadoEvent evento = Assert.IsType<ReporteCreadoEvent>(Assert.Single(_publicador.Eventos));
        Assert.Equal(idReporte, evento.Reporte.Id);
        Assert.Equal(_geoespacial.IdCoordenadaDevuelto, evento.Reporte.IdCoordenada);
    }

    [Fact]
    public async Task Handle_NoLlamaAGeoespacial_SiElReporteEsInvalido()
    {
        CrearReporteCommand comando = ComandoValido() with { Descripcion = "" };

        await Assert.ThrowsAsync<ArgumentException>(() => CrearHandler().Handle(comando, CancellationToken.None));

        Assert.Empty(_geoespacial.Llamadas);
        Assert.Empty(_repositorio.Agregados);
    }

    [Fact]
    public async Task Handle_GuardaYPublicaElUsuarioEnviado()
    {
        Guid idUsuario = Guid.CreateVersion7();

        await CrearHandler().Handle(ComandoValido() with { IdUsuario = idUsuario }, CancellationToken.None);

        Assert.Equal(idUsuario, Assert.Single(_repositorio.Agregados).IdUsuario);
        ReporteCreadoEvent evento = Assert.IsType<ReporteCreadoEvent>(Assert.Single(_publicador.Eventos));
        Assert.Equal(idUsuario, evento.Reporte.IdUsuario);
    }

    [Fact]
    public async Task Handle_AsignaUnUsuarioGenerico_SiNoSeEnviaUsuario()
    {
        await CrearHandler().Handle(ComandoValido(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, Assert.Single(_repositorio.Agregados).IdUsuario);
    }

    [Fact]
    public async Task Handle_NoLlamaAGeoespacial_SiElUsuarioEsVacio()
    {
        CrearReporteCommand comando = ComandoValido() with { IdUsuario = Guid.Empty };

        await Assert.ThrowsAsync<ArgumentException>(() => CrearHandler().Handle(comando, CancellationToken.None));

        Assert.Empty(_geoespacial.Llamadas);
    }

    [Fact]
    public async Task Handle_ConArchivo_SubeLaImagenAMultimediaYGuardaSuUrlYNombre()
    {
        CrearReporteCommand comando = ComandoValido() with
        {
            UrlImagen = null,
            Imagen = new ImagenAdjunta(new MemoryStream(ValidadorImagenTests.Png), ValidadorImagenTests.Png.Length, @"C:\fakepath\foto.png")
        };

        await CrearHandler().Handle(comando, CancellationToken.None);

        Reporte guardado = Assert.Single(_repositorio.Agregados);
        Assert.Equal("foto.png", guardado.NombreImagen);
        Assert.Equal(_multimedia.UrlImagenDevuelta, guardado.UrlImagen);

        (string nombreArchivo, double? latitud, double? longitud) = Assert.Single(_multimedia.Llamadas);
        Assert.Equal("foto.png", nombreArchivo);
        Assert.Equal(4.6512, latitud);
        Assert.Equal(-74.0561, longitud);
    }

    [Fact]
    public async Task Handle_NoLlamaAGeoespacialNiAMultimedia_SiElArchivoNoEsUnaImagen()
    {
        byte[] texto = "no soy una imagen"u8.ToArray();
        CrearReporteCommand comando = ComandoValido() with
        {
            UrlImagen = null,
            Imagen = new ImagenAdjunta(new MemoryStream(texto), texto.Length, "foto.png")
        };

        await Assert.ThrowsAsync<ImagenInvalidaException>(() => CrearHandler().Handle(comando, CancellationToken.None));

        Assert.Empty(_multimedia.Llamadas);
        Assert.Empty(_geoespacial.Llamadas);
        Assert.Empty(_repositorio.Agregados);
    }

    [Fact]
    public async Task Handle_NoLlamaAGeoespacialNiGuarda_SiMultimediaNoEstaDisponible()
    {
        _multimedia.ExcepcionALanzar = new MultimediaNoDisponibleException("caído");
        CrearReporteCommand comando = ComandoValido() with
        {
            UrlImagen = null,
            Imagen = new ImagenAdjunta(new MemoryStream(ValidadorImagenTests.Png), ValidadorImagenTests.Png.Length, "foto.png")
        };

        await Assert.ThrowsAsync<MultimediaNoDisponibleException>(() => CrearHandler().Handle(comando, CancellationToken.None));

        Assert.Empty(_geoespacial.Llamadas);
        Assert.Empty(_repositorio.Agregados);
        Assert.Empty(_publicador.Eventos);
    }

    [Fact]
    public async Task Handle_NoGuarda_SiMultimediaRechazaLaImagen()
    {
        _multimedia.ExcepcionALanzar = new ImagenInvalidaException("tamaño inválido");
        CrearReporteCommand comando = ComandoValido() with
        {
            UrlImagen = null,
            Imagen = new ImagenAdjunta(new MemoryStream(ValidadorImagenTests.Png), ValidadorImagenTests.Png.Length, "foto.png")
        };

        await Assert.ThrowsAsync<ImagenInvalidaException>(() => CrearHandler().Handle(comando, CancellationToken.None));

        Assert.Empty(_geoespacial.Llamadas);
        Assert.Empty(_repositorio.Agregados);
    }

    [Fact]
    public async Task Handle_NoGuardaNiPublica_SiGeoespacialNoEstaDisponible()
    {
        _geoespacial.ExcepcionALanzar = new GeoespacialNoDisponibleException("caído");

        await Assert.ThrowsAsync<GeoespacialNoDisponibleException>(
            () => CrearHandler().Handle(ComandoValido(), CancellationToken.None));

        Assert.Empty(_repositorio.Agregados);
        Assert.Empty(_publicador.Eventos);
    }

    [Fact]
    public async Task Handle_NoGuardaNiPublica_SiLaCoordenadaEsInvalida()
    {
        _geoespacial.ExcepcionALanzar = new CoordenadaInvalidaException("fuera de Bogotá");

        await Assert.ThrowsAsync<CoordenadaInvalidaException>(
            () => CrearHandler().Handle(ComandoValido(), CancellationToken.None));

        Assert.Empty(_repositorio.Agregados);
        Assert.Empty(_publicador.Eventos);
    }

    private sealed class GeoespacialFalso : IGeoespacialClient
    {
        public Guid IdCoordenadaDevuelto { get; } = Guid.NewGuid();
        public Exception? ExcepcionALanzar { get; set; }
        public List<(Guid IdReporte, double Latitud, double Longitud)> Llamadas { get; } = [];

        public Task<Guid> AsignarCoordenadaAsync(Guid idReporte, double latitud, double longitud, CancellationToken cancellationToken)
        {
            Llamadas.Add((idReporte, latitud, longitud));
            return ExcepcionALanzar is null ? Task.FromResult(IdCoordenadaDevuelto) : Task.FromException<Guid>(ExcepcionALanzar);
        }
    }

    private sealed class MultimediaFalso : IMultimediaClient
    {
        public string UrlImagenDevuelta { get; } = "https://multimedia.urbanalert.com/" + Guid.NewGuid() + ".jpg";
        public Exception? ExcepcionALanzar { get; set; }
        public List<(string NombreArchivo, double? Latitud, double? Longitud)> Llamadas { get; } = [];

        public Task<string> SubirImagenAsync(
            Stream contenido, long tamano, string nombreArchivo, string tipoContenido,
            double? latitud, double? longitud, CancellationToken cancellationToken)
        {
            Llamadas.Add((nombreArchivo, latitud, longitud));
            return ExcepcionALanzar is null
                ? Task.FromResult(UrlImagenDevuelta)
                : Task.FromException<string>(ExcepcionALanzar);
        }
    }

    private sealed class RepositorioFalso : IReporteRepository
    {
        public List<Reporte> Agregados { get; } = [];

        public Task AgregarAsync(Reporte reporte, CancellationToken cancellationToken)
        {
            Agregados.Add(reporte);
            return Task.CompletedTask;
        }

        public Task<Reporte?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<(IReadOnlyList<Reporte> Elementos, int Total)> ObtenerPaginadoAsync(
            EstadoReporte? estado, NivelEmergencia? nivelEmergencia, TipoReporte? tipo, int pagina, int tamanoPagina, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ActualizarAsync(Reporte reporte, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task EliminarAsync(Reporte reporte, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class PublicadorFalso : IEventPublisher
    {
        public List<object> Eventos { get; } = [];

        public Task PublicarAsync<TEvento>(TEvento evento, CancellationToken cancellationToken) where TEvento : class
        {
            Eventos.Add(evento);
            return Task.CompletedTask;
        }
    }
}
