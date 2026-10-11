using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.DTOs.Reportes;
using Application.Comun;
using Domain.Reportes;

namespace IntegrationTests.Reportes;

public class ReportesEndpointsTests : IClassFixture<ReportesApiFactory>
{
    // Mismas reglas que la API: enums como códigos UPPER_SNAKE_CASE, sin números.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(CodigoEnum.Politica, allowIntegerValues: false) }
    };

    private readonly HttpClient _client;

    public ReportesEndpointsTests(ReportesApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static CrearReporteRequest ReporteDePrueba() => new(
        CategoriaReporte.ViasYAndenes,
        TipoReporte.HuecosEnLaVia,
        "Hueco grande que afecta el tránsito vehicular",
        4.6512,
        -74.0561,
        "https://imagenes.urbanalert.com/foto.jpg");

    private async Task<Guid> CrearReporteAsync()
    {
        HttpResponseMessage respuesta = await _client.PostAsJsonAsync("/api/v1/Reportes", ReporteDePrueba(), Json);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        CrearReporteRespuesta? creado = await respuesta.Content.ReadFromJsonAsync<CrearReporteRespuesta>();
        Assert.NotNull(creado);

        return creado!.IdReporte;
    }

    [Fact]
    public async Task CrearYObtenerReporte_PersisteEnPostgresConValoresPorDefecto()
    {
        Guid idReporte = await CrearReporteAsync();

        HttpResponseMessage respuestaConsulta = await _client.GetAsync($"/api/v1/Reportes/{idReporte}");
        Assert.Equal(HttpStatusCode.OK, respuestaConsulta.StatusCode);

        ReporteRespuesta? reporte = await respuestaConsulta.Content.ReadFromJsonAsync<ReporteRespuesta>();
        Assert.NotNull(reporte);
        Assert.Equal(idReporte, reporte!.Id);
        Assert.Equal("VIAS_Y_ANDENES", reporte.Categoria);
        Assert.Equal("HUECOS_EN_LA_VIA", reporte.Tipo);
        Assert.Equal("DEFAULT", reporte.NivelEmergencia);
        Assert.Equal("REPORTADO", reporte.Estado);
        Assert.NotEqual(Guid.Empty, reporte.IdCoordenada);
        Assert.Null(reporte.IdResponsable);
        Assert.Null(reporte.MotivoRechazo);
    }

    [Fact]
    public async Task CrearReporte_Devuelve400_SiLaCoordenadaEstaFueraDeBogota()
    {
        CrearReporteRequest fueraDeBogota = ReporteDePrueba() with { Latitud = 6.2442, Longitud = -75.5812 };

        HttpResponseMessage respuesta = await _client.PostAsJsonAsync("/api/v1/Reportes", fueraDeBogota, Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task ObtenerReportes_IncluyeLosReportesCreados()
    {
        Guid idReporte = await CrearReporteAsync();

        HttpResponseMessage respuesta = await _client.GetAsync("/api/v1/Reportes");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        PaginaRespuesta? pagina = await respuesta.Content.ReadFromJsonAsync<PaginaRespuesta>();
        Assert.NotNull(pagina);
        Assert.Contains(pagina!.Elementos, r => r.Id == idReporte);
    }

    [Fact]
    public async Task ObtenerReportes_FiltraPorEstado()
    {
        Guid idReporte = await CrearReporteAsync();

        HttpResponseMessage respuesta = await _client.GetAsync("/api/v1/Reportes?estado=RECHAZADO");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        PaginaRespuesta? pagina = await respuesta.Content.ReadFromJsonAsync<PaginaRespuesta>();
        Assert.NotNull(pagina);
        Assert.DoesNotContain(pagina!.Elementos, r => r.Id == idReporte);
    }

    [Fact]
    public async Task ObtenerReportes_FiltraPorTipo()
    {
        Guid idHueco = await CrearReporteAsync();
        Guid idSemaforo = await CrearReporteDeTipoAsync(CategoriaReporte.Senalizacion, TipoReporte.SemaforoApagado);

        PaginaRespuesta pagina = await ObtenerPaginaAsync("?tipo=SEMAFORO_APAGADO&tamanoPagina=100");

        Assert.Contains(pagina.Elementos, r => r.Id == idSemaforo);
        Assert.DoesNotContain(pagina.Elementos, r => r.Id == idHueco);
        Assert.All(pagina.Elementos, r => Assert.Equal("SEMAFORO_APAGADO", r.Tipo));
    }

    [Fact]
    public async Task ObtenerReportes_TotalElementosCuentaSoloLosReportesDelTipo()
    {
        await CrearReporteDeTipoAsync(CategoriaReporte.Senalizacion, TipoReporte.SemaforoApagado);

        PaginaRespuesta delTipo = await ObtenerPaginaAsync("?tipo=SEMAFORO_APAGADO&pagina=1&tamanoPagina=1");
        PaginaRespuesta todos = await ObtenerPaginaAsync("?pagina=1&tamanoPagina=1");

        Assert.True(delTipo.TotalElementos >= 1);
        Assert.True(delTipo.TotalElementos < todos.TotalElementos);
    }

    [Fact]
    public async Task ObtenerReportes_CombinaElFiltroDeTipoConElDeEstado()
    {
        Guid idSemaforo = await CrearReporteDeTipoAsync(CategoriaReporte.Senalizacion, TipoReporte.SemaforoApagado);

        PaginaRespuesta reportados = await ObtenerPaginaAsync("?tipo=SEMAFORO_APAGADO&estado=REPORTADO&tamanoPagina=100");
        PaginaRespuesta rechazados = await ObtenerPaginaAsync("?tipo=SEMAFORO_APAGADO&estado=RECHAZADO&tamanoPagina=100");

        Assert.Contains(reportados.Elementos, r => r.Id == idSemaforo);
        Assert.DoesNotContain(rechazados.Elementos, r => r.Id == idSemaforo);
    }

    [Fact]
    public async Task ObtenerReportes_RespetaElTamanoDePaginaSolicitado()
    {
        await CrearReporteAsync();
        await CrearReporteAsync();

        HttpResponseMessage respuesta = await _client.GetAsync("/api/v1/Reportes?pagina=1&tamanoPagina=1");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        PaginaRespuesta? pagina = await respuesta.Content.ReadFromJsonAsync<PaginaRespuesta>();
        Assert.NotNull(pagina);
        Assert.Single(pagina!.Elementos);
        Assert.True(pagina.TotalElementos >= 2);
    }

    [Fact]
    public async Task ActualizarEstado_AvanzaLaMaquinaDeEstadosYPersisteElCambio()
    {
        Guid idReporte = await CrearReporteAsync();

        HttpResponseMessage respuestaActualizacion = await _client.PatchAsJsonAsync(
            $"/api/v1/Reportes/{idReporte}/estado", new ActualizarEstadoRequest(EstadoReporte.Verificado), Json);
        Assert.Equal(HttpStatusCode.OK, respuestaActualizacion.StatusCode);

        HttpResponseMessage respuestaConsulta = await _client.GetAsync($"/api/v1/Reportes/{idReporte}");
        ReporteRespuesta? reporte = await respuestaConsulta.Content.ReadFromJsonAsync<ReporteRespuesta>();

        Assert.Equal("VERIFICADO", reporte!.Estado);
    }

    [Fact]
    public async Task ActualizarEstado_RechazaUnSaltoDeEstadoConBadRequest()
    {
        Guid idReporte = await CrearReporteAsync();

        HttpResponseMessage respuesta = await _client.PatchAsJsonAsync(
            $"/api/v1/Reportes/{idReporte}/estado", new ActualizarEstadoRequest(EstadoReporte.Asignado), Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task ActualizarNivelEmergencia_PersisteElNuevoNivel()
    {
        Guid idReporte = await CrearReporteAsync();

        HttpResponseMessage respuesta = await _client.PatchAsJsonAsync(
            $"/api/v1/Reportes/{idReporte}/nivel-emergencia", new ActualizarNivelEmergenciaRequest(NivelEmergencia.Alta), Json);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        HttpResponseMessage respuestaConsulta = await _client.GetAsync($"/api/v1/Reportes/{idReporte}");
        ReporteRespuesta? reporte = await respuestaConsulta.Content.ReadFromJsonAsync<ReporteRespuesta>();

        Assert.Equal("ALTA", reporte!.NivelEmergencia);
    }

    [Fact]
    public async Task AsignarResponsable_PersisteElResponsableSinCambiarElEstado()
    {
        Guid idReporte = await CrearReporteAsync();
        Guid idResponsable = Guid.NewGuid();

        HttpResponseMessage respuesta = await _client.PatchAsJsonAsync(
            $"/api/v1/Reportes/{idReporte}/asignacion", new AsignarResponsableRequest(idResponsable));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        HttpResponseMessage respuestaConsulta = await _client.GetAsync($"/api/v1/Reportes/{idReporte}");
        ReporteRespuesta? reporte = await respuestaConsulta.Content.ReadFromJsonAsync<ReporteRespuesta>();

        Assert.Equal(idResponsable, reporte!.IdResponsable);
        Assert.Equal("REPORTADO", reporte.Estado);
    }

    [Fact]
    public async Task RechazarReporte_PersisteElEstadoRechazadoYElMotivo()
    {
        Guid idReporte = await CrearReporteAsync();

        HttpResponseMessage respuesta = await _client.PutAsJsonAsync(
            $"/api/v1/Reportes/{idReporte}/rechazo", new RechazarReporteRequest("Reporte duplicado"));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        HttpResponseMessage respuestaConsulta = await _client.GetAsync($"/api/v1/Reportes/{idReporte}");
        ReporteRespuesta? reporte = await respuestaConsulta.Content.ReadFromJsonAsync<ReporteRespuesta>();

        Assert.Equal("RECHAZADO", reporte!.Estado);
        Assert.Equal("Reporte duplicado", reporte.MotivoRechazo);
    }

    [Fact]
    public async Task EliminarReporte_LoBorraDeLaBaseDeDatos()
    {
        Guid idReporte = await CrearReporteAsync();

        HttpResponseMessage respuestaEliminacion = await _client.DeleteAsync($"/api/v1/Reportes/{idReporte}");
        Assert.Equal(HttpStatusCode.NoContent, respuestaEliminacion.StatusCode);

        HttpResponseMessage respuestaConsulta = await _client.GetAsync($"/api/v1/Reportes/{idReporte}");
        Assert.Equal(HttpStatusCode.NotFound, respuestaConsulta.StatusCode);
    }

    [Fact]
    public async Task ObtenerReporteInexistente_Retorna404()
    {
        HttpResponseMessage respuesta = await _client.GetAsync($"/api/v1/Reportes/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task CrearReporte_AceptaLosCodigosDelCatalogoComoTexto()
    {
        HttpResponseMessage respuesta = await PostJsonAsync("""
            {"categoria":"SENALIZACION","tipo":"SEMAFORO_APAGADO","descripcion":"Semáforo sin luz",
             "latitud":4.6512,"longitud":-74.0561,"urlImagen":"https://imagenes.urbanalert.com/foto.jpg"}
            """);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Fact]
    public async Task CrearReporte_Devuelve400_SiElTipoNoPerteneceALaCategoria()
    {
        HttpResponseMessage respuesta = await _client.PostAsJsonAsync(
            "/api/v1/Reportes", ReporteDePrueba() with { Categoria = CategoriaReporte.Aseo }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task CrearReporte_Devuelve400_SiElTipoNoExisteEnElCatalogo()
    {
        HttpResponseMessage respuesta = await PostJsonAsync("""
            {"categoria":"VIAS_Y_ANDENES","tipo":"BACHE","descripcion":"Bache",
             "latitud":4.6512,"longitud":-74.0561,"urlImagen":"https://imagenes.urbanalert.com/foto.jpg"}
            """);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // PNG mínimo válido (1x1): el servicio valida el formato por su contenido.
    private static readonly byte[] ImagenPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static MultipartFormDataContent FormularioConImagen(
        byte[]? imagen, string nombreArchivo = "foto.png", string? idUsuario = null)
    {
        MultipartFormDataContent formulario = new()
        {
            { new StringContent("VIAS_Y_ANDENES"), "categoria" },
            { new StringContent("HUECOS_EN_LA_VIA"), "tipo" },
            { new StringContent("Hueco reportado desde el teléfono"), "descripcion" },
            { new StringContent("4.6512"), "latitud" },
            { new StringContent("-74.0561"), "longitud" },
        };

        if (idUsuario is not null)
            formulario.Add(new StringContent(idUsuario), "idUsuario");

        if (imagen is not null)
        {
            ByteArrayContent archivo = new(imagen);
            archivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            formulario.Add(archivo, "imagen", nombreArchivo);
        }

        return formulario;
    }

    [Fact]
    public async Task CrearReporte_ConArchivo_SubeLaImagenAMultimediaYGuardaSuUrlYNombre()
    {
        HttpResponseMessage respuesta = await _client.PostAsync("/api/v1/Reportes", FormularioConImagen(ImagenPng, "hueco.png"));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        CrearReporteRespuesta? creado = await respuesta.Content.ReadFromJsonAsync<CrearReporteRespuesta>();

        ReporteRespuesta? reporte = await _client.GetFromJsonAsync<ReporteRespuesta>($"/api/v1/Reportes/{creado!.IdReporte}");

        Assert.Equal("hueco.png", reporte!.NombreImagen);
        Assert.NotNull(reporte.UrlImagen);
        Assert.StartsWith("https://multimedia.urbanalert.com/", reporte.UrlImagen);
    }

    [Fact]
    public async Task CrearReporte_ConJson_GuardaElUsuarioEnviado()
    {
        Guid idUsuario = Guid.CreateVersion7();
        HttpResponseMessage respuesta = await _client.PostAsJsonAsync(
            "/api/v1/Reportes", ReporteDePrueba() with { IdUsuario = idUsuario }, Json);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        CrearReporteRespuesta? creado = await respuesta.Content.ReadFromJsonAsync<CrearReporteRespuesta>();

        ReporteRespuesta? reporte = await _client.GetFromJsonAsync<ReporteRespuesta>($"/api/v1/Reportes/{creado!.IdReporte}");

        Assert.Equal(idUsuario, reporte!.IdUsuario);
    }

    [Fact]
    public async Task CrearReporte_ConArchivo_GuardaElUsuarioEnviado()
    {
        Guid idUsuario = Guid.CreateVersion7();
        HttpResponseMessage respuesta = await _client.PostAsync(
            "/api/v1/Reportes", FormularioConImagen(ImagenPng, idUsuario: idUsuario.ToString()));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        CrearReporteRespuesta? creado = await respuesta.Content.ReadFromJsonAsync<CrearReporteRespuesta>();

        ReporteRespuesta? reporte = await _client.GetFromJsonAsync<ReporteRespuesta>($"/api/v1/Reportes/{creado!.IdReporte}");

        Assert.Equal(idUsuario, reporte!.IdUsuario);
    }

    [Theory]
    [InlineData("no-es-un-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task CrearReporte_ConArchivo_Devuelve400_SiElUsuarioNoEsValido(string idUsuario)
    {
        HttpResponseMessage respuesta = await _client.PostAsync(
            "/api/v1/Reportes", FormularioConImagen(ImagenPng, idUsuario: idUsuario));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task CrearReporte_ConArchivo_Devuelve400_SiNoEsUnaImagen()
    {
        HttpResponseMessage respuesta = await _client.PostAsync("/api/v1/Reportes", FormularioConImagen("no soy una imagen"u8.ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task CrearReporte_ConFormulario_Devuelve400_SiFaltaElArchivo()
    {
        HttpResponseMessage respuesta = await _client.PostAsync("/api/v1/Reportes", FormularioConImagen(imagen: null));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task CrearReporte_ConJson_Devuelve400_SiFaltaLaUrlDeLaImagen()
    {
        HttpResponseMessage respuesta = await PostJsonAsync("""
            {"categoria":"VIAS_Y_ANDENES","tipo":"HUECOS_EN_LA_VIA","descripcion":"Sin imagen",
             "latitud":4.6512,"longitud":-74.0561}
            """);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task ObtenerCatalogo_DevuelveLasCategoriasConSusTipos()
    {
        List<CategoriaRespuesta>? catalogo = await _client.GetFromJsonAsync<List<CategoriaRespuesta>>("/api/v1/Reportes/catalogo");

        Assert.NotNull(catalogo);
        Assert.Equal(10, catalogo!.Count);
        CategoriaRespuesta vias = catalogo[0];
        Assert.Equal("VIAS_Y_ANDENES", vias.Codigo);
        Assert.Equal("Vías y andenes", vias.Nombre);
        Assert.Contains(vias.Tipos, tipo => tipo.Codigo == "HUECOS_EN_LA_VIA" && tipo.Nombre == "Huecos en la vía");
        Assert.Equal(51, catalogo.Sum(categoria => categoria.Tipos.Count));
    }

    [Theory]
    [InlineData("\"categoria\":1,\"tipo\":101")]
    [InlineData("\"categoria\":\"ViasYAndenes\",\"tipo\":\"HuecosEnLaVia\"")]
    public async Task CrearReporte_Devuelve400_SiLosCodigosNoSonUpperSnakeCase(string codigos)
    {
        HttpResponseMessage respuesta = await PostJsonAsync($$"""
            {{{codigos}}},"descripcion":"Hueco",
             "latitud":4.6512,"longitud":-74.0561,"urlImagen":"https://imagenes.urbanalert.com/foto.jpg"}
            """);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("Rechazado")]
    [InlineData("5")]
    [InlineData("NO_EXISTE")]
    public async Task ObtenerReportes_Devuelve400_SiElFiltroNoEsUnCodigoValido(string estado)
    {
        HttpResponseMessage respuesta = await _client.GetAsync($"/api/v1/Reportes?estado={estado}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("BACHE")]
    [InlineData("SemaforoApagado")]
    [InlineData("3")]
    public async Task ObtenerReportes_Devuelve400_SiElTipoNoEsUnCodigoValido(string tipo)
    {
        HttpResponseMessage respuesta = await _client.GetAsync($"/api/v1/Reportes?tipo={tipo}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    private async Task<Guid> CrearReporteDeTipoAsync(CategoriaReporte categoria, TipoReporte tipo)
    {
        HttpResponseMessage respuesta = await _client.PostAsJsonAsync(
            "/api/v1/Reportes", ReporteDePrueba() with { Categoria = categoria, Tipo = tipo }, Json);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        CrearReporteRespuesta? creado = await respuesta.Content.ReadFromJsonAsync<CrearReporteRespuesta>();
        Assert.NotNull(creado);

        return creado!.IdReporte;
    }

    private async Task<PaginaRespuesta> ObtenerPaginaAsync(string consulta)
    {
        HttpResponseMessage respuesta = await _client.GetAsync($"/api/v1/Reportes{consulta}");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        PaginaRespuesta? pagina = await respuesta.Content.ReadFromJsonAsync<PaginaRespuesta>();
        Assert.NotNull(pagina);

        return pagina!;
    }

    private Task<HttpResponseMessage> PostJsonAsync(string json) =>
        _client.PostAsync("/api/v1/Reportes", new StringContent(json, Encoding.UTF8, "application/json"));

    private record CategoriaRespuesta(string Codigo, string Nombre, List<TipoRespuesta> Tipos);

    private record TipoRespuesta(string Codigo, string Nombre);

    private record CrearReporteRespuesta(Guid IdReporte, string Message);

    private record PaginaRespuesta(
        List<ReporteRespuesta> Elementos,
        int Pagina,
        int TamanoPagina,
        int TotalElementos);

    private record ReporteRespuesta(
        Guid Id,
        string Categoria,
        string Tipo,
        string Descripcion,
        Guid IdCoordenada,
        string? UrlImagen,
        string? NombreImagen,
        Guid IdUsuario,
        string NivelEmergencia,
        string Estado,
        DateTime Fecha,
        Guid? IdResponsable,
        string? MotivoRechazo);
}
