namespace Domain.Reportes;

public class TransicionEstadoInvalidaException : InvalidOperationException
{
    public TransicionEstadoInvalidaException(EstadoReporte estadoActual, EstadoReporte estadoSolicitado)
        : base($"No es posible pasar de '{estadoActual}' a '{estadoSolicitado}'.")
    {
    }

    public TransicionEstadoInvalidaException(string message) : base(message) { }
}
