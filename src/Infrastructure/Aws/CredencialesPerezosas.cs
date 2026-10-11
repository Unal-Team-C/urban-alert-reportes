using Amazon.Runtime;
using Amazon.Runtime.Credentials;

namespace Infrastructure.Aws;

// Credenciales de la cadena por defecto del SDK (en ECS, el rol de tarea), resueltas en la primera
// petición firmada y no al crear el HttpClient: así un fallo al obtenerlas solo afecta a las llamadas que
// las necesitan, no a cada controlador. PublicationOnly no guarda la excepción: se reintenta en la siguiente.
internal sealed class CredencialesPerezosas : AWSCredentials
{
    private readonly Lazy<AWSCredentials> _credenciales =
        new(() => DefaultAWSCredentialsIdentityResolver.GetCredentials(), LazyThreadSafetyMode.PublicationOnly);

    public override ImmutableCredentials GetCredentials() => _credenciales.Value.GetCredentials();

    public override Task<ImmutableCredentials> GetCredentialsAsync() => _credenciales.Value.GetCredentialsAsync();
}
