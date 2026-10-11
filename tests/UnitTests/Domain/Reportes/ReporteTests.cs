using Domain.Reportes;

namespace UnitTests.Domain.Reportes;

public class ReporteTests
{
    private const CategoriaReporte Categoria = CategoriaReporte.ViasYAndenes;
    private const TipoReporte Tipo = TipoReporte.HuecosEnLaVia;

    private static Reporte CrearReporteValido() => new(
        Categoria,
        Tipo,
        "Hueco grande que afecta el tránsito vehicular",
        "https://imagenes.urbanalert.com/foto.jpg",
        null,
        Guid.NewGuid());

    [Fact]
    public void Constructor_AsignaValoresPorDefecto()
    {
        DateTime antes = DateTime.UtcNow;

        Reporte reporte = CrearReporteValido();

        DateTime despues = DateTime.UtcNow;

        Assert.NotEqual(Guid.Empty, reporte.Id);
        Assert.Equal(7, reporte.Id.Version);
        Assert.Equal(Categoria, reporte.Categoria);
        Assert.Equal(Tipo, reporte.Tipo);
        Assert.Equal(NivelEmergencia.Default, reporte.NivelEmergencia);
        Assert.Equal(EstadoReporte.Reportado, reporte.Estado);
        Assert.Null(reporte.IdResponsable);
        Assert.Null(reporte.MotivoRechazo);
        Assert.InRange(reporte.Fecha, antes, despues);
    }

    [Theory]
    [InlineData("", "url")]
    [InlineData("descripcion", "")]
    public void Constructor_LanzaExcepcion_SiCamposDeTextoObligatoriosEstanVacios(string descripcion, string urlImagen)
    {
        Assert.Throws<ArgumentException>(() => new Reporte(Categoria, Tipo, descripcion, urlImagen, null, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_LanzaExcepcion_SiElTipoNoPerteneceALaCategoria()
    {
        Assert.Throws<ArgumentException>(() => new Reporte(
            CategoriaReporte.Aseo, TipoReporte.HuecosEnLaVia, "descripcion", "https://imagenes.urbanalert.com/foto.jpg", null, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_LanzaExcepcion_SiLaCategoriaNoExiste()
    {
        Assert.Throws<ArgumentException>(() => new Reporte(
            (CategoriaReporte)99, Tipo, "descripcion", "https://imagenes.urbanalert.com/foto.jpg", null, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_LanzaExcepcion_SiElTipoNoExiste()
    {
        Assert.Throws<ArgumentException>(() => new Reporte(
            Categoria, (TipoReporte)99, "descripcion", "https://imagenes.urbanalert.com/foto.jpg", null, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_DejaElReporteSinCoordenada()
    {
        Reporte reporte = CrearReporteValido();

        Assert.Equal(Guid.Empty, reporte.IdCoordenada);
    }

    [Fact]
    public void AsignarCoordenada_GuardaElIdentificador()
    {
        Reporte reporte = CrearReporteValido();
        Guid idCoordenada = Guid.NewGuid();

        reporte.AsignarCoordenada(idCoordenada);

        Assert.Equal(idCoordenada, reporte.IdCoordenada);
    }

    [Fact]
    public void AsignarCoordenada_LanzaExcepcion_SiIdCoordenadaEsVacio()
    {
        Reporte reporte = CrearReporteValido();

        Assert.Throws<ArgumentException>(() => reporte.AsignarCoordenada(Guid.Empty));
    }

    [Fact]
    public void AsignarCoordenada_LanzaExcepcion_SiYaTieneCoordenada()
    {
        Reporte reporte = CrearReporteValido();
        reporte.AsignarCoordenada(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => reporte.AsignarCoordenada(Guid.NewGuid()));
    }

    [Fact]
    public void AsignarUrlImagen_GuardaLaUrl_SiElReporteNoTeniaUna()
    {
        Reporte reporte = new(Categoria, Tipo, "descripcion", null, "foto.jpg", Guid.NewGuid());

        reporte.AsignarUrlImagen("https://multimedia.urbanalert.com/foto.jpg");

        Assert.Equal("https://multimedia.urbanalert.com/foto.jpg", reporte.UrlImagen);
    }

    [Fact]
    public void AsignarUrlImagen_LanzaExcepcion_SiLaUrlEsVacia()
    {
        Reporte reporte = new(Categoria, Tipo, "descripcion", null, "foto.jpg", Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => reporte.AsignarUrlImagen(""));
    }

    [Fact]
    public void AsignarUrlImagen_LanzaExcepcion_SiLaUrlNoEsAbsoluta()
    {
        Reporte reporte = new(Categoria, Tipo, "descripcion", null, "foto.jpg", Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => reporte.AsignarUrlImagen("no-es-una-url"));
    }

    [Fact]
    public void AsignarUrlImagen_LanzaExcepcion_SiYaTieneUrl()
    {
        Reporte reporte = CrearReporteValido();

        Assert.Throws<InvalidOperationException>(() => reporte.AsignarUrlImagen("https://multimedia.urbanalert.com/foto.jpg"));
    }

    [Fact]
    public void Constructor_LanzaExcepcion_SiIdUsuarioEsVacio()
    {
        Assert.Throws<ArgumentException>(() => new Reporte(Categoria, Tipo, "descripcion", "https://imagenes.urbanalert.com/foto.jpg", null, Guid.Empty));
    }

    [Fact]
    public void Constructor_AceptaSoloElNombreDeLaImagen_SinUrl()
    {
        Reporte reporte = new(Categoria, Tipo, "descripcion", null, "foto.jpg", Guid.NewGuid());

        Assert.Null(reporte.UrlImagen);
        Assert.Equal("foto.jpg", reporte.NombreImagen);
    }

    [Fact]
    public void Constructor_LanzaExcepcion_SiNoHayNiUrlNiNombreDeImagen()
    {
        Assert.Throws<ArgumentException>(() => new Reporte(Categoria, Tipo, "descripcion", null, null, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new Reporte(Categoria, Tipo, "descripcion", " ", "", Guid.NewGuid()));
    }

    [Theory]
    [InlineData("carpeta/foto.jpg")]
    [InlineData("..\\foto.jpg")]
    [InlineData("..")]
    public void Constructor_LanzaExcepcion_SiElNombreDeImagenEsUnaRuta(string nombreImagen)
    {
        Assert.Throws<ArgumentException>(() => new Reporte(Categoria, Tipo, "descripcion", null, nombreImagen, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_LanzaExcepcion_SiUrlImagenNoEsAbsoluta()
    {
        Assert.Throws<ArgumentException>(() => new Reporte(Categoria, Tipo, "descripcion", "no-es-una-url", null, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_LanzaExcepcion_SiDescripcionSuperaLaLongitudMaxima()
    {
        string descripcion = new('a', Reporte.DescripcionMaxLength + 1);

        Assert.Throws<ArgumentException>(() => new Reporte(Categoria, Tipo, descripcion, "https://imagenes.urbanalert.com/foto.jpg", null, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(EstadoReporte.Reportado, EstadoReporte.Verificado)]
    [InlineData(EstadoReporte.Verificado, EstadoReporte.Asignado)]
    [InlineData(EstadoReporte.Asignado, EstadoReporte.EnIntervencion)]
    [InlineData(EstadoReporte.EnIntervencion, EstadoReporte.Resuelto)]
    public void ActualizarEstado_PermiteLaTransicionLinealSiguiente(EstadoReporte estadoInicial, EstadoReporte estadoSiguiente)
    {
        Reporte reporte = CrearReporteValido();
        AvanzarHasta(reporte, estadoInicial);

        reporte.ActualizarEstado(estadoSiguiente);

        Assert.Equal(estadoSiguiente, reporte.Estado);
    }

    [Fact]
    public void ActualizarEstado_LanzaExcepcion_SiSeIntentaSaltarUnEstado()
    {
        Reporte reporte = CrearReporteValido();

        Assert.Throws<TransicionEstadoInvalidaException>(() => reporte.ActualizarEstado(EstadoReporte.Asignado));
    }

    [Fact]
    public void ActualizarEstado_LanzaExcepcion_SiElReporteYaEstaResuelto()
    {
        Reporte reporte = CrearReporteValido();
        AvanzarHasta(reporte, EstadoReporte.Resuelto);

        Assert.Throws<TransicionEstadoInvalidaException>(() => reporte.ActualizarEstado(EstadoReporte.Resuelto));
    }

    [Theory]
    [InlineData(EstadoReporte.Reportado)]
    [InlineData(EstadoReporte.Verificado)]
    [InlineData(EstadoReporte.Asignado)]
    [InlineData(EstadoReporte.EnIntervencion)]
    public void Rechazar_CambiaEstadoYRegistraMotivo_DesdeCualquierEstadoPrevioAResuelto(EstadoReporte estadoInicial)
    {
        Reporte reporte = CrearReporteValido();
        AvanzarHasta(reporte, estadoInicial);

        reporte.Rechazar("Reporte duplicado");

        Assert.Equal(EstadoReporte.Rechazado, reporte.Estado);
        Assert.Equal("Reporte duplicado", reporte.MotivoRechazo);
    }

    [Fact]
    public void Rechazar_LanzaExcepcion_SiElReporteYaEstaResuelto()
    {
        Reporte reporte = CrearReporteValido();
        AvanzarHasta(reporte, EstadoReporte.Resuelto);

        Assert.Throws<TransicionEstadoInvalidaException>(() => reporte.Rechazar("motivo"));
    }

    [Fact]
    public void Rechazar_LanzaExcepcion_SiElMotivoEsVacio()
    {
        Reporte reporte = CrearReporteValido();

        Assert.Throws<ArgumentException>(() => reporte.Rechazar(""));
    }

    [Fact]
    public void Rechazar_LanzaExcepcion_SiElMotivoSuperaLaLongitudMaxima()
    {
        Reporte reporte = CrearReporteValido();
        string motivo = new('a', Reporte.MotivoRechazoMaxLength + 1);

        Assert.Throws<ArgumentException>(() => reporte.Rechazar(motivo));
    }

    [Fact]
    public void AsignarResponsable_EstableceElResponsable_SinCambiarElEstado()
    {
        Reporte reporte = CrearReporteValido();
        Guid idResponsable = Guid.NewGuid();

        reporte.AsignarResponsable(idResponsable);

        Assert.Equal(idResponsable, reporte.IdResponsable);
        Assert.Equal(EstadoReporte.Reportado, reporte.Estado);
    }

    [Fact]
    public void ActualizarNivelEmergencia_CambiaElNivel()
    {
        Reporte reporte = CrearReporteValido();

        reporte.ActualizarNivelEmergencia(NivelEmergencia.Alta);

        Assert.Equal(NivelEmergencia.Alta, reporte.NivelEmergencia);
    }

    private static void AvanzarHasta(Reporte reporte, EstadoReporte estadoObjetivo)
    {
        EstadoReporte[] secuencia =
        [
            EstadoReporte.Verificado,
            EstadoReporte.Asignado,
            EstadoReporte.EnIntervencion,
            EstadoReporte.Resuelto
        ];

        foreach (EstadoReporte estado in secuencia)
        {
            if (reporte.Estado == estadoObjetivo)
            {
                return;
            }

            reporte.ActualizarEstado(estado);
        }
    }
}
