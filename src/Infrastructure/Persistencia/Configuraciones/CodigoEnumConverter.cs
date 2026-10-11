using Application.Comun;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Persistencia.Configuraciones;

// Guarda los enums con el mismo código UPPER_SNAKE_CASE que expone la API.
public class CodigoEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    valor => CodigoEnum.ACodigo(valor),
    codigo => CodigoEnum.DesdeCodigo<TEnum>(codigo))
    where TEnum : struct, Enum;
