namespace Application.Imagenes;

// Hereda de ArgumentException para que se responda como cualquier otra entrada inválida (400).
public class ImagenInvalidaException(string message) : ArgumentException(message);
