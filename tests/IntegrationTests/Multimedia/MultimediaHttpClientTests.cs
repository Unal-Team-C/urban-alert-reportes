using System.Net;
using System.Text;
using System.Text.Json;
using Application.Imagenes;
using Application.Multimedia;
using Infrastructure.Multimedia;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntegrationTests.Multimedia;

public class MultimediaHttpClientTests
{
    private static string Invocacion(int statusCode, string cuerpoJson) => $$"""
        {"statusCode":{{statusCode}},"body":{{JsonSerializer.Serialize(cuerpoJson)}},"headers":{},"isBase64Encoded":false}
        """;

    private static string ImagenSubida(string imageUrl) => $$"""
        {"imageId":"7f3c2a9e-1b4d-4c6e-9a8f-2d1e0b5c7a31","imageUrl":"{{imageUrl}}","contentType":"image/png","sizeBytes":825,"uploadedAt":"2026-10-10T01:53:26Z"}
        """;

    [Fact]
    public async Task SubirImagenAsync_DevuelveLaUrl_SiMultimediaResponde201()
    {
        ManejadorFalso manejador = new(_ =>
            Respuesta(HttpStatusCode.OK, Invocacion(201, ImagenSubida("http://localhost:9000/urbanalert-images/foo.png"))));

        string url = await CrearCliente(manejador).SubirImagenAsync(
            new MemoryStream([1, 2, 3]), 3, "foto.png", "image/png", 4.6512, -74.0561, CancellationToken.None);

        Assert.Equal("http://localhost:9000/urbanalert-images/foo.png", url);
    }

    [Fact]
    public async Task SubirImagenAsync_InvocaElRuntimeInterfaceEmulatorConElEventoDeApiGateway()
    {
        ManejadorFalso manejador = new(_ =>
            Respuesta(HttpStatusCode.OK, Invocacion(201, ImagenSubida("http://localhost:9000/urbanalert-images/foo.png"))));

        await CrearCliente(manejador).SubirImagenAsync(
            new MemoryStream([1, 2, 3]), 3, "foto.png", "image/png", 4.6512, -74.0561, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, manejador.UltimaSolicitud!.Method);
        Assert.Equal(
            "http://multimedia.test/2015-03-31/functions/function/invocations",
            manejador.UltimaSolicitud.RequestUri!.ToString());

        using JsonDocument evento = JsonDocument.Parse(manejador.UltimoCuerpo!);
        Assert.Equal("/api/v1/multimedia/images", evento.RootElement.GetProperty("rawPath").GetString());
        Assert.True(evento.RootElement.GetProperty("isBase64Encoded").GetBoolean());
        Assert.Equal("POST", evento.RootElement.GetProperty("requestContext").GetProperty("http").GetProperty("method").GetString());

        byte[] cuerpoMultipart = Convert.FromBase64String(evento.RootElement.GetProperty("body").GetString()!);
        string multipart = Encoding.UTF8.GetString(cuerpoMultipart);
        Assert.Contains("name=\"lat\"", multipart);
        Assert.Contains("4.6512", multipart);
        Assert.Contains("name=\"image\"; filename=\"foto.png\"", multipart);
        Assert.Contains("Content-Type: image/png", multipart);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(413)]
    [InlineData(415)]
    public async Task SubirImagenAsync_LanzaImagenInvalida_SiMultimediaRechazaLaImagen(int statusCode)
    {
        ManejadorFalso manejador = new(_ => Respuesta(HttpStatusCode.OK,
            Invocacion(statusCode, """{"code":415,"message":"Unsupported image format"}""")));

        ImagenInvalidaException ex = await Assert.ThrowsAsync<ImagenInvalidaException>(
            () => CrearCliente(manejador).SubirImagenAsync(
                new MemoryStream([1, 2, 3]), 3, "foto.png", "image/png", null, null, CancellationToken.None));

        Assert.Contains("Unsupported image format", ex.Message);
    }

    [Fact]
    public async Task SubirImagenAsync_LanzaNoDisponible_SiMultimediaResponde503()
    {
        ManejadorFalso manejador = new(_ => Respuesta(HttpStatusCode.OK,
            Invocacion(503, """{"code":503,"message":"Service unavailable"}""")));

        await Assert.ThrowsAsync<MultimediaNoDisponibleException>(
            () => CrearCliente(manejador).SubirImagenAsync(
                new MemoryStream([1, 2, 3]), 3, "foto.png", "image/png", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task SubirImagenAsync_LanzaNoDisponible_SiElInvocadorNoResponde200()
    {
        ManejadorFalso manejador = new(_ => Respuesta(HttpStatusCode.BadGateway, "no disponible"));

        await Assert.ThrowsAsync<MultimediaNoDisponibleException>(
            () => CrearCliente(manejador).SubirImagenAsync(
                new MemoryStream([1, 2, 3]), 3, "foto.png", "image/png", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task SubirImagenAsync_LanzaNoDisponible_SiNoHayConexion()
    {
        ManejadorFalso manejador = new(_ => throw new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<MultimediaNoDisponibleException>(
            () => CrearCliente(manejador).SubirImagenAsync(
                new MemoryStream([1, 2, 3]), 3, "foto.png", "image/png", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task SubirImagenAsync_LanzaNoDisponible_SiSeAgotaElTiempo()
    {
        ManejadorFalso manejador = new(_ => throw new TaskCanceledException("timeout", new TimeoutException()));

        await Assert.ThrowsAsync<MultimediaNoDisponibleException>(
            () => CrearCliente(manejador).SubirImagenAsync(
                new MemoryStream([1, 2, 3]), 3, "foto.png", "image/png", null, null, CancellationToken.None));
    }

    private static MultimediaHttpClient CrearCliente(ManejadorFalso manejador) =>
        new(new HttpClient(manejador) { BaseAddress = new Uri("http://multimedia.test/") },
            NullLogger<MultimediaHttpClient>.Instance);

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
