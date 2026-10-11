namespace Application.Geoespacial;

public class GeoespacialNoDisponibleException(string message, Exception? innerException = null)
    : Exception(message, innerException);
