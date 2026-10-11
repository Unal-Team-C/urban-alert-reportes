using System.Net;
using System.Net.Http.Json;
using Amazon.Runtime;
using AwsSignatureVersion4;

namespace UnitTests.Infrastructure;

// En la nube, Reportes llama a la ruta interna de Geoespacial por el API Gateway, que exige firma IAM (SigV4).
public class FirmaGeoespacialTests
{
    private static readonly SessionAWSCredentials Credenciales = new("AKIDEXAMPLE", "wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY", "token-de-sesion");

    [Fact]
    public async Task AwsSignatureHandler_FirmaLaPeticionParaExecuteApi_ConCredencialesDeSesion()
    {
        ManejadorCaptura captura = new();
        AwsSignatureHandler firma = new(new AwsSignatureHandlerSettings("us-east-2", "execute-api", Credenciales))
        {
            InnerHandler = captura
        };
        using HttpClient cliente = new(firma) { BaseAddress = new Uri("https://abc123.execute-api.us-east-2.amazonaws.com/") };

        await cliente.PostAsJsonAsync("api/v1/geoespacial/coordinates", new { reportId = Guid.Empty });

        Dictionary<string, string> cabeceras = Assert.Single(captura.Cabeceras);
        Assert.StartsWith("AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/", cabeceras["Authorization"]);
        Assert.Contains("/us-east-2/execute-api/aws4_request", cabeceras["Authorization"]);
        Assert.Equal("token-de-sesion", cabeceras["X-Amz-Security-Token"]);
    }

    private sealed class ManejadorCaptura : HttpMessageHandler
    {
        // El manejador de firma quita sus cabeceras al terminar el envío: se copian en el momento.
        public List<Dictionary<string, string>> Cabeceras { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Cabeceras.Add(request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
        }
    }
}
