using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.DTOs.Reportes;
using Application.Captcha;
using Application.Comun;
using Application.Interfaces.Captcha;
using Domain.Reportes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntegrationTests.Reportes;

// Host con la verificación humana activa y un verificador controlable (sin red a Turnstile).
public class CaptchaApiFactory : ReportesApiFactory
{
    public const string TokenValido = "token-valido";

    public VerificadorControlable Verificador { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Captcha:Habilitado", "true");
        builder.UseSetting("Captcha:ClaveSecreta", "1x0000000000000000000000000000000AA");
        builder.ConfigureTestServices(services =>
            services.RemoveAll<IVerificadorCaptcha>().AddSingleton<IVerificadorCaptcha>(Verificador));
    }

    public sealed class VerificadorControlable : IVerificadorCaptcha
    {
        public bool ProveedorCaido { get; set; }

        public Task VerificarAsync(string? token, CancellationToken cancellationToken)
        {
            if (ProveedorCaido)
                throw new CaptchaNoDisponibleException("proveedor caído (prueba)");
            if (token != TokenValido)
                throw new CaptchaInvalidoException(token is null ? "missing-input-response" : "invalid-input-response");
            return Task.CompletedTask;
        }
    }
}

// Spec 003 RF-001, RF-005: la creación por JSON y por multipart exige la prueba cuando la verificación está activa.
public class CaptchaEndpointsTests(CaptchaApiFactory factory) : IClassFixture<CaptchaApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(CodigoEnum.Politica, allowIntegerValues: false) }
    };

    // PNG mínimo válido (1×1) para la variante multipart.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private Task<HttpResponseMessage> CrearJsonAsync(string? token)
    {
        factory.Verificador.ProveedorCaido = false;
        return factory.CreateClient().PostAsJsonAsync("/api/v1/Reportes", new CrearReporteRequest(
            CategoriaReporte.ViasYAndenes,
            TipoReporte.HuecosEnLaVia,
            "Hueco grande que afecta el tránsito vehicular",
            4.6512,
            -74.0561,
            "https://imagenes.urbanalert.com/foto.jpg",
            CaptchaToken: token), Json);
    }

    private Task<HttpResponseMessage> CrearMultipartAsync(string? token)
    {
        MultipartFormDataContent formulario = new()
        {
            { new StringContent("VIAS_Y_ANDENES"), "categoria" },
            { new StringContent("HUECOS_EN_LA_VIA"), "tipo" },
            { new StringContent("Hueco grande que afecta el tránsito vehicular"), "descripcion" },
            { new StringContent("4.6512"), "latitud" },
            { new StringContent("-74.0561"), "longitud" }
        };
        if (token is not null)
            formulario.Add(new StringContent(token), "captchaToken");
        ByteArrayContent imagen = new(Png);
        imagen.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formulario.Add(imagen, "imagen", "foto.png");
        return factory.CreateClient().PostAsync("/api/v1/Reportes", formulario);
    }

    [Fact]
    public async Task Json_ConPruebaValida_Crea201()
    {
        Assert.Equal(HttpStatusCode.Created, (await CrearJsonAsync(CaptchaApiFactory.TokenValido)).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("token-falso")]
    public async Task Json_SinPruebaOInvalida_Responde400ConMensaje(string? token)
    {
        HttpResponseMessage respuesta = await CrearJsonAsync(token);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("verificar que la solicitud la hizo una persona", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Multipart_ConPruebaValida_Crea201()
    {
        factory.Verificador.ProveedorCaido = false;
        Assert.Equal(HttpStatusCode.Created, (await CrearMultipartAsync(CaptchaApiFactory.TokenValido)).StatusCode);
    }

    [Fact]
    public async Task Multipart_SinPrueba_Responde400()
    {
        factory.Verificador.ProveedorCaido = false;
        Assert.Equal(HttpStatusCode.BadRequest, (await CrearMultipartAsync(null)).StatusCode);
    }

    [Fact]
    public async Task ConElProveedorCaido_Responde503()
    {
        factory.Verificador.ProveedorCaido = true;
        HttpResponseMessage respuesta = await factory.CreateClient().PostAsJsonAsync("/api/v1/Reportes", new CrearReporteRequest(
            CategoriaReporte.ViasYAndenes,
            TipoReporte.HuecosEnLaVia,
            "Hueco grande que afecta el tránsito vehicular",
            4.6512,
            -74.0561,
            "https://imagenes.urbanalert.com/foto.jpg",
            CaptchaToken: CaptchaApiFactory.TokenValido), Json);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        factory.Verificador.ProveedorCaido = false;
    }
}
