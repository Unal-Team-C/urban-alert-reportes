using Domain.Reportes;

namespace Application.Reportes.Catalogo;

public record TipoReporteDto(TipoReporte Codigo, string Nombre);

public record CategoriaReporteDto(CategoriaReporte Codigo, string Nombre, IReadOnlyList<TipoReporteDto> Tipos)
{
    public static IReadOnlyList<CategoriaReporteDto> DesdeCatalogo() =>
        CatalogoReportes.Categorias
            .Select(categoria => new CategoriaReporteDto(
                categoria.Categoria,
                categoria.Nombre,
                categoria.Tipos.Select(tipo => new TipoReporteDto(tipo.Tipo, tipo.Nombre)).ToList()))
            .ToList();
}
