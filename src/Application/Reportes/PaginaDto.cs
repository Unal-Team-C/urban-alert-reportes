namespace Application.Reportes;

public record PaginaDto<T>(IReadOnlyList<T> Elementos, int Pagina, int TamanoPagina, int TotalElementos);
