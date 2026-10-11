using System.Text.Json;

namespace Application.Comun;

// Convención de la plataforma para los valores de enums fuera del código
// (JSON, parámetros de consulta, base de datos): UPPER_SNAKE_CASE.
// Ejemplo: EstadoReporte.EnIntervencion <-> "EN_INTERVENCION".
public static class CodigoEnum
{
    public static JsonNamingPolicy Politica { get; } = JsonNamingPolicy.SnakeCaseUpper;

    public static string ACodigo<TEnum>(TEnum valor) where TEnum : struct, Enum =>
        Politica.ConvertName(valor.ToString());

    public static bool TryParse<TEnum>(string? codigo, out TEnum valor) where TEnum : struct, Enum
    {
        valor = default;
        return codigo is not null && Codigos<TEnum>.PorCodigo.TryGetValue(codigo, out valor);
    }

    public static TEnum DesdeCodigo<TEnum>(string codigo) where TEnum : struct, Enum =>
        TryParse(codigo, out TEnum valor)
            ? valor
            : throw new InvalidOperationException($"'{codigo}' no es un código válido de {typeof(TEnum).Name}.");

    private static class Codigos<TEnum> where TEnum : struct, Enum
    {
        public static readonly Dictionary<string, TEnum> PorCodigo =
            Enum.GetValues<TEnum>().ToDictionary(ACodigo, valor => valor, StringComparer.Ordinal);
    }
}
