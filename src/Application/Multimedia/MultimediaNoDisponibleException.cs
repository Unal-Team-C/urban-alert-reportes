namespace Application.Multimedia;

public class MultimediaNoDisponibleException(string message, Exception? innerException = null)
    : Exception(message, innerException);
