namespace Application.Geoespacial;

// Hereda de ArgumentException para que se responda como cualquier otra entrada inválida (400).
public class CoordenadaInvalidaException(string message) : ArgumentException(message);
