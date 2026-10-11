using System.Net;
using System.Text;
using System.Text.Json;
using Application.Geoespacial;
using Infrastructure.Geoespacial;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntegrationTests.Geoespacial;

public class GeoespacialHttpClientTests
{
    private static readonly Guid IdReporte = Guid.Parse("01a10e93-3634-7d68-a682-6c048c82882f");
    private static readonly Guid IdCoordenada = Guid.Parse("b13c0cbd-fdb0-4b77-b152-f665d463a15e");

    private static string LocationAssigned() => $$$"""
        {"coordinateId":"{{{IdCoordenada}}}","reportId":"{{{IdReporte}}}","coordinate":{"lat":4.6512,"lon":-74.0561}}
        """;

    [Theory]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.OK)]
    public async Task AsignarCoordenada_DevuelveElIdentificador_SiGeoespacialAsigna(HttpStatusCode codigo)
    {
        ManejadorFalso manejador = new(_ => Respuesta(codigo, LocationAssigned()));

        Guid idCoordenada = await CrearCliente(manejador).AsignarCoordenadaAsync(IdReporte, 4.6512, -74.0561, CancellationToken.None);

        Assert.Equal(IdCoordenada, idCoordenada);
    }

    [Fact]
    public async Task AsignarCoordenada_EnviaElContratoDeGeoespacial()
    {
        ManejadorFalso manejador = new(_ => Respuesta(HttpStatusCode.Created, LocationAssigned()));

        await CrearCliente(manejador).AsignarCoordenadaAsync(IdReporte, 4.6512, -74.0561, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, manejador.UltimaSolicitud!.Method);
        Assert.Equal("http://geoespacial.test/api/v1/geospatial/coordinates", manejador.UltimaSolicitud.RequestUri!.ToString());

        using JsonDocument cuerpo = JsonDocument.Parse(manejador.UltimoCuerpo!);
        Assert.Equal(IdReporte, cuerpo.RootElement.GetProperty("reportId").GetGuid());
        Assert.Equal(4.6512, cuerpo.RootElement.GetProperty("coordinate").GetProperty("lat").GetDouble());
        Assert.Equal(-74.0561, cuerpo.RootElement.GetProperty("coordinate").GetProperty("lon").GetDouble());
    }

    [Fact]
    public async Task AsignarCoordenada_LanzaCoordenadaInvalida_Si400()
    {
        ManejadorFalso manejador = new(_ => Respuesta(HttpStatusCode.BadRequest, """
            {"code":400,"message":"Invalid parameter","details":[{"field":"coordinate","reason":"coordinate is outside the Bogotá range"}]}
            """));

        CoordenadaInvalidaException ex = await Assert.ThrowsAsync<CoordenadaInvalidaException>(
            () => CrearCliente(manejador).AsignarCoordenadaAsync(IdReporte, 6.24, -75.58, CancellationToken.None));

        Assert.Contains("outside the Bogotá range", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task AsignarCoordenada_LanzaNoDisponible_SiGeoespacialFalla(HttpStatusCode codigo)
    {
        ManejadorFalso manejador = new(_ => Respuesta(codigo, """{"code":0,"message":"error"}"""));

        await Assert.ThrowsAsync<GeoespacialNoDisponibleException>(
            () => CrearCliente(manejador).AsignarCoordenadaAsync(IdReporte, 4.6512, -74.0561, CancellationToken.None));
    }

    [Fact]
    public async Task AsignarCoordenada_LanzaNoDisponible_SiNoHayConexion()
    {
        ManejadorFalso manejador = new(_ => throw new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<GeoespacialNoDisponibleException>(
            () => CrearCliente(manejador).AsignarCoordenadaAsync(IdReporte, 4.6512, -74.0561, CancellationToken.None));
    }

    [Fact]
    public async Task AsignarCoordenada_LanzaNoDisponible_SiSeAgotaElTiempo()
    {
        ManejadorFalso manejador = new(_ => throw new TaskCanceledException("timeout", new TimeoutException()));

        await Assert.ThrowsAsync<GeoespacialNoDisponibleException>(
            () => CrearCliente(manejador).AsignarCoordenadaAsync(IdReporte, 4.6512, -74.0561, CancellationToken.None));
    }

    private static GeoespacialHttpClient CrearCliente(ManejadorFalso manejador) =>
        new(new HttpClient(manejador) { BaseAddress = new Uri("http://geoespacial.test/") },
            NullLogger<GeoespacialHttpClient>.Instance);

    private static HttpResponseMessage Respuesta(HttpStatusCode codigo, string json) =>
        new(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class ManejadorFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? UltimaSolicitud { get; private set; }
        public string? UltimoCuerpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UltimaSolicitud = request;
            UltimoCuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }
}
